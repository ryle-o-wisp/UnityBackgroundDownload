#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using UnityEngine;

namespace Unity.Networking
{
	class BackgroundDownloadEditor : BackgroundDownload
	{
		static HttpClient _client;
		float _progress;

		[RuntimeInitializeOnLoadMethod]
		static void Init()
		{
			_client = new HttpClient();
		}

		public BackgroundDownloadEditor(BackgroundDownloadConfig config)
			: base(config)
		{
			Start(config);
		}

		private async void Start(BackgroundDownloadConfig config)
		{
			_status = BackgroundDownloadStatus.Downloading;
			_error = "";
			_progress = 0f;

			var persistentFilePath = Path.Combine(_persistentDataPath, config.filePath);
			try
			{
				using (var str = await _client.GetStreamAsync(_config.url))
				using (var stream = new FileStream(persistentFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
				{
					await str.CopyToAsync(stream);
				}

				_progress = 1f;
				_status = BackgroundDownloadStatus.Done;
				_error = "";
			}
			catch (Exception e)
			{
				_error = $"{e.GetType()} during download: {e.Message}";
				_status = BackgroundDownloadStatus.Failed;
			}
		}

		public override bool keepWaiting => _status == BackgroundDownloadStatus.Downloading;

		protected override float GetProgress() => _progress;

		internal static Dictionary<string, BackgroundDownload> LoadDownloads()
		{
			return new Dictionary<string, BackgroundDownload>();
		}

		internal static void SaveDownloads(Dictionary<string, BackgroundDownload> downloads) { }

		public override void Dispose() { base.Dispose(); }
	}
}

#endif
