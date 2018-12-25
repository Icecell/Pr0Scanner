using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using Newtonsoft.Json.Linq;
using Tesseract;

namespace Pr0grammScanner
{
    class Pr0grammScannerMain
    {
        private List<Thread> threads = new List<Thread>();
        private List<Worker> workers = new List<Worker>();
        public Settings Settings { get; set; }
        public ConcurrentQueue<JobResult> ResultQueue { get; set; } = new ConcurrentQueue<JobResult>();
        public ScannerStatus ScannerStatus { get; set; } = ScannerStatus.Startup;
        public ConcurrentQueue<Job> jobQueue { get; set; } = new ConcurrentQueue<Job>();

        private volatile bool stop;

        public bool Stop
        {
            get { return stop; }
            set { stop = value; }
        }

        public Pr0grammScannerMain()
        {
        }

        public void Run()
        {
            fillJobQueue();

            if (!Stop && startup())
            {
                ScannerStatus = ScannerStatus.Running;
                while (jobQueue.Count > 0 && !Stop)
                {
                    Thread.Sleep(50);
                }
            }

            shutdown();
        }

        private JObject getNextJson(int older)
        {
            string topString = Settings.Top ? "promoted=1&" : "";
            string olderString = older > 0 ? $"older={older}&" : "";
            string tagsString = HttpUtility.UrlEncode(Settings.SearchTag).Replace("%20", "+");
            string requestUrl = $"https://pr0gramm.com/api/items/get?flags=1&{topString}{olderString}tags={tagsString}";
            using (HttpWebResponse httpWebReponse = (HttpWebResponse)HttpWebRequest.Create(requestUrl).GetResponse())
            {
                var responseString = new StreamReader(httpWebReponse.GetResponseStream()).ReadToEnd();
                return JObject.Parse(responseString);
            }
        }

        private void fillJobQueue()
        {
            Console.WriteLine("fillJobQueue ...");
            int idOrPromoted = -1;
            do
            {
                var json = getNextJson(idOrPromoted);
                var tokens = json["items"];
                idOrPromoted = 0;
                foreach (var token in tokens)
                {
                    if(Settings.MaxPics > 0 && Settings.MaxPics <= jobQueue.Count)
                    {
                        Console.WriteLine($"LimitFetch {Settings.MaxPics} reached.");
                        return;
                    }

                    idOrPromoted = (int)(Settings.Top ? token["promoted"] : token["id"]);
                    var imgUrl = (string)token["image"];
                    if (Regex.IsMatch(imgUrl.ToLower(), @"\.(jpg|jpeg|png|tiff|bmp)$"))
                    {
                        var directUrl = $"https://img.pr0gramm.com/{imgUrl}";
                        var browserUrl = $"https://pr0gramm.com/new/{idOrPromoted}";
                        jobQueue.Enqueue(new Job(directUrl, browserUrl));
                    }
                }

                Console.WriteLine($"Jobs: {jobQueue.Count}");

                if(Stop)
                {
                    Console.WriteLine("Abort fetching jobs.");
                    return;
                }

                Thread.Sleep(500); // Slow down next request
            } while (idOrPromoted != 0);

            Console.WriteLine("Fetching jobs done");
        }

        private bool startup()
        {
            ScannerStatus = ScannerStatus.Startup;
            Console.WriteLine("Startup");
            for (int i = 0; i < Settings.Threads; i++)
            {
                TesseractEngine tesseractEngine = null;
                try
                {
                    tesseractEngine = new TesseractEngine(Settings.TesseractEngineDataFolder, "eng", EngineMode.Default);
                }
                catch (System.Exception e)
                {
                    Console.WriteLine(e.Message);
                    return false;
                }

                var worker = new Worker(Settings, tesseractEngine, jobQueue, ResultQueue);
                Thread thread = new Thread(new ThreadStart(worker.Run));
                thread.Start();
                threads.Add(thread);
                workers.Add(worker);
            }
            return true;
        }

        private void shutdown()
        {
            ScannerStatus = ScannerStatus.Stopping;
            Console.WriteLine("Stopping ...");
            foreach (var worker in workers)
                worker.Stop = true;

            foreach (var thread in threads)
                thread.Join();

            Console.WriteLine("Stopped.");
            ScannerStatus = ScannerStatus.Stopped;
        }
    }
}
