using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications
{
    public class PlatformDeleteMessage
    {
        public string Message { get; set; }
        public Series Series { get; set; }
        public bool DeletedFiles { get; set; }
        public string DeletedFilesMessage { get; set; }

        public override string ToString()
        {
            return Message;
        }

        public PlatformDeleteMessage(Series series, bool deleteFiles)
        {
            Series = series;
            DeletedFiles = deleteFiles;
            DeletedFilesMessage = DeletedFiles ?
                "Platform removed and all files were deleted" :
                "Platform removed, files were not deleted";
            Message = series.Title + " - " + DeletedFilesMessage;
        }
    }
}
