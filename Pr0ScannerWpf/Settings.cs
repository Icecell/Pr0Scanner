using System;
using System.IO;
using Newtonsoft.Json;

namespace Pr0grammScanner
{
    internal class Settings
    {
        public static string GetFilename()
        {
            return typeof(Settings).Name.ToLower() + ".json";
        }

        public static Settings Load()
        {
            try
            {
                using (StreamReader sr = new StreamReader(GetFilename()))
                    return JsonConvert.DeserializeObject<Settings>(sr.ReadToEnd());
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Using default settings.");
                Settings s = new Settings();
                s.Save();
                return s;
            }
        }

        public void Save()
        {
            try
            {
                var filename = GetFilename();
                Console.WriteLine($"Writing file: {filename}");
                using (StreamWriter sw = new StreamWriter(filename))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    var js = new JsonSerializer();
                    js.Formatting =  Newtonsoft.Json.Formatting.Indented;
                    js.Serialize(writer, this);
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        // Only used internally to tell Worker.cs perfect pic size based on monitor settings
        private float scalingFactor = 1;
        public float GetScalingFactor() { return scalingFactor; }
        public void SetScalingFactor(float scalingFactor) { this.scalingFactor = scalingFactor; }

        public string SearchTag { get; set; } = @"!d:2018:12 & (spendet | spenden)";
        public bool Top { get; set; } = false; // Set false to find "new" posts
        public int Threads { get; set; } = 4; // More threads, more CPU usage but faster processing
        public int MaxPics { get; set; } = 0; // 0 = no limit
        public float MinValue { get; set; } = 5; // min value to find
        public float MaxValue { get; set; } = 1000; // max value to find
        public int PreviewPicSize { get; set; } = 150; // higher value means more RAM-usage
        public string RegexFind { get; set; } = @"\d+([,.]\d+)?\s?(EUR|€|Euro|EURO)"; // Finds values like "1,00 €" or "123,45EUR" or "100 Euro"
        public string RegexExtract { get; set; } = @"\d+([,.]\d+)?"; // Take values like "1,00" or "123,45" or "100"
        public string TesseractEngineDataFolder { get; set; } = "tessdata"; // Folder to trained tessdata
    }
}