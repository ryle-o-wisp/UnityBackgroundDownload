#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using UnityEngine;

namespace Unity.Networking
{
	class BackgroundDownloadEditor : BackgroundDownload
	{
		static HttpClient _client;
		private readonly CancellationTokenSource _tokenSource;
		private long _contentLength;

		[RuntimeInitializeOnLoadMethod]
		static void Init()
		{
			_client = new HttpClient();
		}

		public BackgroundDownloadEditor(BackgroundDownloadConfig config)
			: base(config)
		{
			_tokenSource = new CancellationTokenSource();
			Start(config);
		}

		private async void Start(BackgroundDownloadConfig config)
		{
			_status = BackgroundDownloadStatus.Downloading;
			_error = "";
			_contentLength = 0u;

			var persistentFilePath = Path.Combine(Application.persistentDataPath, config.filePath);
			try
			{
				using (var response = await _client.GetAsync(_config.url, HttpCompletionOption.ResponseHeadersRead, _tokenSource.Token))
				{
					response.EnsureSuccessStatusCode();

					_contentLength = response.Content.Headers.ContentLength ?? 0;

					using (var stream = new FileStream(persistentFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
					{
						await response.Content.CopyToAsync(stream);
					}
				}

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

		protected override float GetProgress()
		{
			if (_status != BackgroundDownloadStatus.Downloading) return 1.0f;

			var file = new FileInfo(Path.Combine(Application.persistentDataPath, config.filePath));
			if (file.Exists && _contentLength > 0)
			{
				return (float) file.Length / _contentLength;
			}
			return 0f;
		}

		internal static Dictionary<string, BackgroundDownload> LoadDownloads()
		{
			return new Dictionary<string, BackgroundDownload>();
		}

		internal static void SaveDownloads(Dictionary<string, BackgroundDownload> downloads) { }

		public override void Dispose()
		{
			_tokenSource.Cancel();
			_tokenSource.Dispose();

			base.Dispose();
		}
	}
}

#endif
