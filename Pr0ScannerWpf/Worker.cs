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

namespace Pr0grammScanner
{
    internal class Worker : IDisposable
    {
        private Settings settings;
        private TesseractEngine tesseractEngine;
        private ConcurrentQueue<Job> jobQueue;
        private ConcurrentQueue<JobResult> resultQueue;

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

        // Downloads pic from job-url, scans pic for value and when found, added to resultQueue
        public void DoJob(Job job)
        {
            Bitmap bitmap = getBitmapFromWeb(job.DirectPicUrl);

            // If bitmap is too tiny, double size
            if(bitmap.Width < 1000 || bitmap.Height < 1000)
            {
                var oldBitmap = bitmap;
                bitmap = new Bitmap(oldBitmap, bitmap.Width * 2, bitmap.Height * 2);
                oldBitmap.Dispose();
            }

            Pix pix = null;
            try
            {
                pix = new BitmapToPixConverter().Convert(bitmap);
                // Avoid warning
                pix.XRes = 70;
                pix.YRes = 70;

                Page page = tesseractEngine.Process(pix, PageSegMode.Auto);
                string text = page.GetText();
                page.Dispose();

                //Console.WriteLine(text);

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
                    if(valueitem >= settings.MinValue)
                    {
                        resultValue = valueitem;
                        break;
                    }
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
                result.Value = resultValue;
                resultQueue.Enqueue(result);

                Console.WriteLine($"{resultValue} {job.BrowserUrl}");
            }
            finally
            {
                bitmap.Dispose();
                pix?.Dispose();
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