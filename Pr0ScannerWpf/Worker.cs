using System;
using Tesseract;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Threading;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;

namespace Pr0grammScanner
{
    internal class Worker : IDisposable
    {
        private Settings settings;
        private TesseractEngine tesseractEngine;
        private ConcurrentQueue<Job> jobQueue;
        private ConcurrentQueue<JobResult> resultQueue;

        public static int ExceptionWorkerCount = 0; // HACK

        public Worker(Settings settings, TesseractEngine tesseractEngine, ConcurrentQueue<Job> jobQueue, ConcurrentQueue<JobResult> resultQueue)
        {
            this.settings = settings;
            this.tesseractEngine = tesseractEngine;
            this.jobQueue = jobQueue;
            this.resultQueue = resultQueue;
        }

        private volatile bool stop;

        public bool Stop
        {
            get { return stop; }
            set { stop = value; }
        }

        private Bitmap getBitmapFromWeb(string url)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(url);
            HttpWebResponse httpWebReponse = (HttpWebResponse)httpWebRequest.GetResponse();
            Stream stream = httpWebReponse.GetResponseStream();
            var image = new Bitmap(Image.FromStream(stream));
            stream.Dispose();
            return image;
        }

        private string GetTextFromBitmap(Bitmap bitmap)
        {
            Bitmap scaledBitmap = null;
            // If bitmap is too tiny, double size
            if (bitmap.Width < 1000 || bitmap.Height < 1000)
            {
                scaledBitmap = new Bitmap(bitmap, bitmap.Width * 2, bitmap.Height * 2);
                bitmap = scaledBitmap;
            }

            using (Pix pix = new BitmapToPixConverter().Convert(bitmap))
            {
                if (scaledBitmap != null)
                    scaledBitmap.Dispose();

                // Avoid warning
                pix.XRes = 70;
                pix.YRes = 70;

                using (Page page = tesseractEngine.Process(pix, PageSegMode.Auto))
                {
                    return page.GetText();
                }
            }
        }

        private float GetValueFromBitmap(Bitmap bitmap)
        {
            var text = GetTextFromBitmap(bitmap);

            var matches = Regex.Matches(text, settings.RegexFind);
            List<float> values = new List<float>();
            foreach (var matchObj in matches)
            {
                var match = (Match)matchObj;
                var stringMatch = match.Value;

                var matchValue = Regex.Match(text.Substring(match.Index), settings.RegexExtract);
                CultureInfo ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                ci.NumberFormat.CurrencyDecimalSeparator = ",";
                var valueReplaced = matchValue.Value.Replace('.', ',');
                float value = float.Parse(valueReplaced, NumberStyles.Any, ci);

                if (value > 0)
                    values.Add(value);
            }
            values.Sort();

            float resultValue = 0;
            foreach (var valueitem in values)
            {
                if (valueitem >= settings.MinValue)
                {
                    resultValue = valueitem;
                    break;
                }
            }

            return resultValue;
        }

        private List<Tag> GetTags(int id)
        {
            string requestUrl = $"https://pr0gramm.com/api/items/info?itemId={id}";
            Thread.Sleep(2000); // Avoid 503 response
            using (HttpWebResponse httpWebReponse = (HttpWebResponse)HttpWebRequest.Create(requestUrl).GetResponse())
            {
                var responseString = new StreamReader(httpWebReponse.GetResponseStream()).ReadToEnd();
                return JsonConvert.DeserializeObject<List<Tag>>(JObject.Parse(responseString)["tags"].ToString());
            }
        }

        private float GetValueFromTags(int id)
        {
            var tags = GetTags(id).OrderBy(tag2 => tag2.confidence).ToList();
            var tag = tags.FirstOrDefault(tag2 => Regex.Matches(tag2.tag, settings.RegexFind).Count > 0);
            if (tag != null)
            {
                var matchValue = Regex.Match(tag.tag, settings.RegexExtract);
                CultureInfo ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                ci.NumberFormat.CurrencyDecimalSeparator = ",";
                var valueReplaced = matchValue.Value.Replace('.', ',');
                float value = float.Parse(valueReplaced, NumberStyles.Any, ci);
                return value;
            }
            else
            {
                return 0;
            }
        }

        // Downloads pic from job-url, scans pic for value and when found, added to resultQueue
        public void DoJob(Job job)
        {
            Bitmap bitmap = getBitmapFromWeb(job.DirectPicUrl);
            
            try
            {
                float value = GetValueFromBitmap(bitmap);
                bool valueFromTag = false;
                if (value <= 0)
                {
                    valueFromTag = true;
                    value = GetValueFromTags(job.ImageId);
                }

                var result = new JobResult();
                int sizeOfPic = (int)Math.Round(settings.PreviewPicSize * settings.GetScalingFactor());
                var size = new Size(sizeOfPic, sizeOfPic);
                if (bitmap.Height >= bitmap.Width)
                    size.Height = (int)Math.Round(sizeOfPic * ((float)bitmap.Height / bitmap.Width));
                else
                    size.Width = (int)Math.Round(sizeOfPic * ((float)bitmap.Width / bitmap.Height));
                var scaledBitmap = new Bitmap(bitmap, size);

                result.Bitmap = scaledBitmap;
                result.Url = job.BrowserUrl;
                result.Value = value;
                resultQueue.Enqueue(result);

                Console.WriteLine($"{value} {job.BrowserUrl} {valueFromTag}");
            }
            finally
            {
                bitmap.Dispose();
            }
        }

        public void Run()
        {
            while (!Stop)
            {
                Job job = null;
                try
                {
                    do
                    {
                        if (!jobQueue.TryDequeue(out job))
                            Thread.Sleep(100);
                    } while (job == null && !Stop);

                    if (job != null)
                        DoJob(job);
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex);
                    Console.WriteLine(job?.DirectPicUrl);
                    Worker.ExceptionWorkerCount++;
                }
            } // while

            Dispose();
        }

        public void Dispose()
        {
            tesseractEngine.Dispose();
        }
    }
}