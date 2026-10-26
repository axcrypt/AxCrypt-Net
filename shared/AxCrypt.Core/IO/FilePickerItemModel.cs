using System;
using System.Collections.Generic;
using System.Linq;
using AxCrypt.Core.UI.ViewModel;

namespace AxCrypt.Core.IO
{
    [Obsolete("Use FilePickerItemViewModel instead.")]
    public class FilePickerItemModel : ViewModelBase
    {
        private string _encryptedAxCryptFileImage = "axcicon";
        private string _fileImage = "fileimg";
        private string _folderImage = "icofldr";

        private const string AXCRYPT_FILE_EXTENSION = "axx";

        public const string GOOGLEFOLDER_MIMETYPE = "application/vnd.google-apps.folder";

        public string FileName { get; set; }

        public string ImportFullName { get; set; }

        public string FileID { get; set; }

        public bool IsFolder { get; set; }

        public string FileSize { get; set; }

        public long FileSizeInBytes { get; set; }

        public string ModifiedTime { get; set; }

        public string MimeType { get; set; }

        public string FileExtension { get; set; }

        public FileProvider Source { get; set; }

        public string ParentPath { get; set; } = string.Empty;

        public string Image
        {
            get
            {
                if (IsFolder)
                {
                    return _folderImage;
                }

                if (FileExtension != null && IsAxCryptFile(FileExtension))
                {
                    return _encryptedAxCryptFileImage;
                }

                return _fileImage;
            }

            private set { }
        }

        private bool IsAxCryptFile(string fileExtension)
        {
            return fileExtension == AXCRYPT_FILE_EXTENSION || fileExtension.EndsWith(AXCRYPT_FILE_EXTENSION);
        }

        public bool IsSelected
        {
            get { return GetProperty<bool>(nameof(IsSelected)); }
            set { SetProperty(nameof(IsSelected), value); }
        }

        public bool IsGoogleFileType
        {
            get => GoogleFileMimeType();
        }

        // Returns true when the file was created by a Google Workspace app.
        private bool GoogleFileMimeType()
        {
            if (MimeType == null)
            {
                return false;
            }

            if (GoogleMIMETypes.Contains(MimeType))
            {
                return true;
            }

            return MimeType.StartsWith("application/vnd.google-apps");
        }

        // https://developers.google.com/drive/api/v3/mime-types
        private static IEnumerable<string> GoogleMIMETypes = new List<string>
        {
            "application/vnd.google-apps.audio",
            "application/vnd.google-apps.document",
            "application/vnd.google-apps.drive-sdk",
            "application/vnd.google-apps.drawing",
            "application/vnd.google-apps.file",
            "application/vnd.google-apps.folder",
            "application/vnd.google-apps.form",
            "application/vnd.google-apps.fusiontable",
            "application/vnd.google-apps.jam",
            "application/vnd.google-apps.map",
            "application/vnd.google-apps.photo",
            "application/vnd.google-apps.presentation",
            "application/vnd.google-apps.script",
            "application/vnd.google-apps.shortcut",
            "application/vnd.google-apps.site",
            "application/vnd.google-apps.spreadsheet",
            "application/vnd.google-apps.unknown",
            "application/vnd.google-apps.video",
        };
    }

    [Obsolete("Use CloudFileProviderUtility.FileProvider instead.")]
    public enum FileProvider
    {
        Local,

        PhoneBrowser,

        GoogleDrive,

        DropBox,

        OneDrive,

        iCloud,
    }
}

