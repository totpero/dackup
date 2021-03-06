using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using Serilog;

using Aliyun.OSS;

namespace dackup
{
    public class AliyunOssStorage : StorageBase
    {
        private string endpoint, accessKeyId, accessKeySecret, bucketName;
        private AliyunOssStorage() { }
        public string PathPrefix{get;set;}
        public DateTime? RemoveThreshold{get;set;}
        public AliyunOssStorage(string endpoint, string accessKeyId, string accessKeySecret, string bucketName)
        {
            this.endpoint = endpoint;
            this.accessKeyId = accessKeyId;
            this.accessKeySecret = accessKeySecret;
            this.bucketName = bucketName;
        }
        protected override UploadResult Upload(string fileName)
        {
            OssClient client = new OssClient(endpoint, accessKeyId, accessKeySecret);
            string key = this.PathPrefix + $"/{DateTime.Now:yyyy_MM_dd_HH_mm_ss}/" + fileName.Replace(DackupContext.Current.TmpPath,string.Empty).TrimStart('/');
            key = key.Trim('/');
            
            Log.Information($"Upload to aliyun file: {fileName} key: {key} pathPrefix: {this.PathPrefix}");

            client.PutObject(bucketName, key, fileName);
            return new UploadResult();
        }
        protected override PurgeResult Purge()
        {
            if (RemoveThreshold == null || RemoveThreshold.Value > DateTime.Now)
            {
                return new PurgeResult();
            }

            Log.Information($"Purge to aliyun  removeThreshold: {RemoveThreshold}");

            OssClient client = new OssClient(endpoint, accessKeyId, accessKeySecret);
            var objectListing = client.ListObjects(bucketName, this.PathPrefix);

            var objectsToDelete = new List<string>();

            foreach (var summary in objectListing.ObjectSummaries)
            {
                if (summary.LastModified.ToUniversalTime() <= RemoveThreshold.Value)
                {
                    objectsToDelete.Add(summary.Key);
                }
            }

            if (objectsToDelete.Count == 0)
            {
                Log.Information("Nothing to purge.");
            }
            else
            {
                objectsToDelete.ForEach(item =>
                {
                    Log.Information($"Prepare to purge: {item}");
                });

                DeleteObjectsRequest request = new DeleteObjectsRequest(bucketName, objectsToDelete);
                client.DeleteObjects(request);

                Log.Information("Aliyun oss purge done.");
            }
            return new PurgeResult();
        }
    }
}