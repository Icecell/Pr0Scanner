namespace Pr0grammScanner
{
    internal class Job
    {
        public Job(string directPicUrl, string browserUrl, int imageId)
        {
            this.DirectPicUrl = directPicUrl;
            this.BrowserUrl = browserUrl;
            this.ImageId = imageId;
        }

        public string DirectPicUrl { get; private set; }
        public string BrowserUrl { get; private set; }
        public int ImageId { get; private set; }
    }
}