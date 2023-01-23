#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Unity.Networking
{
	class BackgroundDownloadEditor : BackgroundDownload
	{
		private static HttpClient _client;
		private CancellationTokenSource _tokenSource;
		private long _contentLength;

		[RuntimeInitializeOnLoadMethod]
		static void Init()
		{
			_client = new HttpClient();
			_client.Timeout = TimeSpan.FromDays(1);
			ServicePointManager.DefaultConnectionLimit = 6;

			EditorApplication.playModeStateChanged += change =>
			{
				if (change == PlayModeStateChange.ExitingPlayMode) _client.Dispose();
			};
		}

		public BackgroundDownloadEditor(BackgroundDownloadConfig config)
			: base(config)
		{
			_tokenSource = new CancellationTokenSource();
			StartDownloading();
		}

		private async void StartDownloading()
		{
			_status = BackgroundDownloadStatus.Downloading;
			_error = "";
			_contentLength = 0u;

			var persistentFilePath = Path.Combine(Application.persistentDataPath, _config.filePath);
			try
			{
				var token = _tokenSource.Token;

				if (_config.url.ToString().StartsWith("file://"))
				{
					var sourceFilePath = _config.url.ToString()["file://".Length..];
					await using var target = new FileStream(persistentFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
					await using var source = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
					await source.CopyToAsync(target, 4096, token);
				}
				else
				{
					using var response = await _client.GetAsync(_config.url, HttpCompletionOption.ResponseHeadersRead, token);
					response.EnsureSuccessStatusCode();

					_contentLength = response.Content.Headers.ContentLength ?? 0;

					await using var target = new FileStream(persistentFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
					await using var stream = await response.Content.ReadAsStreamAsync();
					await stream.CopyToAsync(target, 4096, token);
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
			_tokenSource?.Cancel();
			_tokenSource?.Dispose();
			_tokenSource = null;

			if (_status != BackgroundDownloadStatus.Done)
			{
				File.Delete(Path.Combine(Application.persistentDataPath, config.filePath));
			}

			base.Dispose();
		}
	}
}

#endif
