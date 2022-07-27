using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;

namespace Retalix.sgw.Model.Catalog
{
    public class ItemImage
    {
        public ItemImage(CloudBlob cloudBlob)
        {
            ImageBytes = cloudBlob.DownloadByteArray();
            ContentType = cloudBlob.Properties.ContentType;
        }

        public byte[] ImageBytes { get; private set; }
        public string ContentType { get; private set; }
    }
}
