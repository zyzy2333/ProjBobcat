﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.GameResource;
using ProjBobcat.Event;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.ResourceInfoResolver
{
    public class AssetInfoResolver : IResourceInfoResolver
    {
        private string _basePath;
        public string AssetIndexUriRoot { get; set; }
        public string AssetUriRoot { get; set; } = "https://resources.download.minecraft.net/";

        public string BasePath
        {
            get => _basePath.TrimEnd('\\');
            set => _basePath = value;
        }

        public VersionInfo VersionInfo { get; set; }

        public event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;

        public IEnumerable<IGameResource> ResolveResource()
        {
            return ResolveResourceTaskAsync().GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<IGameResource>> ResolveResourceTaskAsync()
        {
            LogGameResourceInfoResolveStatus("开始进行游戏资源(Asset)检查");
            if (string.IsNullOrEmpty(VersionInfo?.AssetInfo.Url)) return default;
            if (string.IsNullOrEmpty(VersionInfo?.AssetInfo.Id)) return default;

            var assetIndexesDi =
                new DirectoryInfo(Path.Combine(GamePathHelper.GetAssetsRoot(BasePath), "indexes"));
            var assetObjectsDi =
                new DirectoryInfo(Path.Combine(GamePathHelper.GetAssetsRoot(BasePath), "objects"));

            if (!assetIndexesDi.Exists) assetIndexesDi.Create();
            if (!assetObjectsDi.Exists) assetObjectsDi.Create();

            var assetIndexesPath = Path.Combine(assetIndexesDi.FullName, $"{VersionInfo.AssetInfo.Id}.json");
            if (!File.Exists(assetIndexesPath))
            {
                LogGameResourceInfoResolveStatus("没有发现Asset Indexes 文件， 开始下载");
                var assetIndexDownloadUri = VersionInfo.AssetInfo.Url;
                if (!string.IsNullOrEmpty(AssetIndexUriRoot))
                {
                    var assetIndexUriRoot = HttpHelper.RegexMatchUri(VersionInfo.AssetInfo.Url).TrimEnd('/');
                    assetIndexDownloadUri =
                        $"{AssetIndexUriRoot}{assetIndexDownloadUri.Substring(assetIndexUriRoot.Length)}";
                }

                var indexDownloadResult = await DownloadHelper.DownloadSingleFileAsync(new Uri(assetIndexDownloadUri),
                    assetIndexesDi.FullName,
                    $"{VersionInfo.AssetInfo.Id}.json").ConfigureAwait(false);
                if (indexDownloadResult.TaskStatus != TaskResultStatus.Success)
                {
                    LogGameResourceInfoResolveStatus($"Asset Indexes 文件下载失败！原因{indexDownloadResult.Message}",
                        LogType.Error);
                    return default;
                }

                LogGameResourceInfoResolveStatus("Asset Indexes 文件下载完成", LogType.Success);
            }

            LogGameResourceInfoResolveStatus("开始解析Asset Indexes 文件...");

            AssetObjectModel assetObject;
            try
            {
                var content = File.ReadAllText(assetIndexesPath);
                assetObject = JsonConvert.DeserializeObject<AssetObjectModel>(content);
            }
            catch (Exception ex)
            {
                LogGameResourceInfoResolveStatus($"解析Asset Indexes 文件失败！原因：{ex.Message}", LogType.Error);
                return default;
            }

            if (assetObject == null)
            {
                LogGameResourceInfoResolveStatus("解析Asset Indexes 文件失败！原因：未知错误", LogType.Error);
                return default;
            }

            var lostAssets = (from asset in assetObject.Objects
                    let hash = asset.Value.Hash
                    let twoDigitsHash = hash.Substring(0, 2)
                    let path = Path.Combine(assetObjectsDi.FullName, twoDigitsHash)
                    where !File.Exists(Path.Combine(path, asset.Value.Hash))
                    select new AssetDownloadInfo
                    {
                        Title = hash,
                        Path = path,
                        Type = "Asset",
                        Uri = $"{AssetUriRoot}{twoDigitsHash}/{asset.Value.Hash}",
                        FileSize = asset.Value.Size,
                        CheckSum = hash,
                        FileName = hash
                    })
                .Cast<IGameResource>().ToList();

            LogGameResourceInfoResolveStatus("Assets 解析完成", LogType.Success);
            return lostAssets;
        }

        private void LogGameResourceInfoResolveStatus(string currentStatus, LogType logType = LogType.Normal)
        {
            GameResourceInfoResolveEvent?.Invoke(this, new GameResourceInfoResolveEventArgs
            {
                CurrentProgress = currentStatus,
                LogType = logType
            });
        }
    }
}