namespace Pr0grammScanner
{
    internal class Job
    {
        public Job(string directPicUrl, string browserUrl)
        {
            this.DirectPicUrl = directPicUrl;
            this.BrowserUrl = browserUrl;
        }

        public string DirectPicUrl { get; private set; }
        public string BrowserUrl { get; private set; }
    }
}