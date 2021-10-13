namespace DownloadConsole.Models
{
	public class DCConfig
	{
		public string OutputDir { get; set; } = "";
		public string SourcesFile { get; set; } = "";
		public bool UseCustomThreads { get; set; } = false;
		public int DownloadThreads { get; set; } = 0;
		public int SearchThreads { get; set; } = 0;
		public bool DownloadThumbnails { get; set; } = false;
		public bool AttachThumbnails { get; set; } = true;
	}
}