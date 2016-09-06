﻿using NicoPlayerHohoema.Util;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace NicoPlayerHohoema.Models
{
	abstract public class DividedQualityNicoVideo : BindableBase
	{
		// Note: ThumbnailResponseが初期化されていないと利用できない

		public NicoVideoQuality Quality { get; private set; }
		public NicoVideo NicoVideo { get; private set; }
		protected NicoVideoDownloadContext _Context;


		public string RawVideoId
		{
			get
			{
				return NicoVideo.RawVideoId;
			}
		}


		private NicoVideoCacheState? _CacheState;
		public NicoVideoCacheState? CacheState
		{
			get { return _CacheState; }
			protected set { SetProperty(ref _CacheState, value); }
		}


		
		/// <summary>
		/// このクオリティは利用可能か
		/// (オリジナル画質しか存在しない動画の場合、true)
		/// </summary>
		abstract public bool IsAvailable { get; }

		public uint CacheProgressSize { get; private set; }

		public VideoDownloadProgress Progress { get; private set; }

		protected FileAccessor<VideoDownloadProgress> _DownloadProgressFileAccessor;

		NicoVideoDownloader _NicoVideoDownloader;

		public DividedQualityNicoVideo(NicoVideoQuality quality, NicoVideo nicoVideo, NicoVideoDownloadContext context)
		{
			Quality = quality;
			NicoVideo = nicoVideo;
			_Context = context;

			_DownloadProgressFileAccessor = new FileAccessor<VideoDownloadProgress>(_Context.VideoSaveFolder, ProgressFileName);
		}

		public async Task SetupDownloadProgress()
		{
			// DLが途中の場合はこのロードが成功しProgressが埋まる
			Progress = await _DownloadProgressFileAccessor.Load();

			if (Progress == null)
			{
				Progress = new VideoDownloadProgress(VideoSize);
			}
			else
			{
				CacheProgressSize = Progress.BufferedSize();
			}

			await CheckCacheStatus();
		}



		abstract public string VideoFileName { get; }

		abstract public string ProgressFileName { get; }



		abstract public bool CanRequestDownload { get; }

		public abstract uint VideoSize { get; }


		public bool CanPlay
		{
			get
			{
				return CanRequestDownload
					|| CacheState == NicoVideoCacheState.Cached;
			}
		}

		public bool NowPlaying
		{
			get
			{
				return _Context.CheckVideoPlaying(RawVideoId, Quality);
			}
		}

		public bool IsCached
		{
			get
			{
				return CacheState == NicoVideoCacheState.Cached;
			}
		}

		public bool HasCache
		{
			get
			{
				return CacheState.HasValue;
			}
		}


		private bool _IsCacheRequested;
		public bool IsCacheRequested
		{
			get { return _IsCacheRequested; }
			protected set { SetProperty(ref _IsCacheRequested, value); }
		}



		public Task<bool> ExistVideo()
		{
			return _Context.VideoSaveFolder.ExistFile(VideoFileName);
		}


		internal Task SaveProgress()
		{
			return _DownloadProgressFileAccessor.Save(Progress);
		}


		internal async Task CheckCacheStatus()
		{
			if (!IsAvailable)
			{
				CacheState = null;
			}

			IsCacheRequested = _Context.CheckCacheRequested(this.RawVideoId, Quality);

			if (IsCacheRequested)
			{
				var existVideo = await ExistVideo();
				if (existVideo
					&& (Progress.CheckComplete()))
				{
					CacheState = NicoVideoCacheState.Cached;
				}
				else if (_Context.CheckVideoDownloading(this.RawVideoId, Quality))
				{
					CacheState = NicoVideoCacheState.NowDownloading;
				}
				else if (existVideo)
				{
					CacheState = NicoVideoCacheState.CacheProgress;
				}
				else // if (NicoVideoCachedStream.ExistIncompleteOriginalQuorityVideo(CachedWatchApiResponse, saveFolder))
				{
					CacheState = null;
				}

				OnPropertyChanged(nameof(CanRequestDownload));
				OnPropertyChanged(nameof(CanPlay));
				OnPropertyChanged(nameof(IsCached));
				OnPropertyChanged(nameof(CanPlay));
			}
			else
			{
				CacheState = null;
			}
		}


		internal async Task<NicoVideoDownloader> CreateDownloader()
		{
			if (!IsAvailable)
			{
				throw new Exception("");
			}

			if (!CanPlay && !CanRequestDownload)
			{
				throw new Exception("");
			}


			var file = await GetCacheFile();
			var downloader = new NicoVideoDownloader(
				this
				, NicoVideo.HohoemaApp.NiconicoContext.HttpClient
				, NicoVideo.CachedWatchApiResponse
				, file
				);

			System.Diagnostics.Debug.WriteLine($"size:{downloader.Size}");

			AddCacheEventHandler(downloader);

			return downloader;
		}

		

		public async Task DeleteCache()
		{
			Debug.Write($"{NicoVideo.Title}:{Quality.ToString()}のキャッシュを削除開始...");

			await CancelCacheRequest();

			await DeleteCacheFile();

			await DeleteDownloadProgress();

			await CheckCacheStatus();

			Debug.WriteLine($".完了");
		}

		public async Task RequestCache()
		{
			if (!IsAvailable) { return; }

			if (_Context.CheckCacheRequested(NicoVideo.RawVideoId, Quality))
			{
				return;
			}

			await _Context.RequestDownload(NicoVideo.RawVideoId, Quality);

			await CheckCacheStatus();


			await NicoVideo.OnCacheRequested();
		}

		protected async Task DeleteCacheFile()
		{
			if (!IsAvailable) { return; }

			var saveFolder = _Context.VideoSaveFolder;
			var fileName = VideoFileName;
			try
			{
				if (await saveFolder.ExistFile(fileName))
				{
					var file = await saveFolder.GetFileAsync(fileName);
					await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
				}
			}
			catch { }
		}

		protected Task DeleteDownloadProgress()
		{
			if (!IsAvailable) { return Task.CompletedTask; }

			return _DownloadProgressFileAccessor.Delete();
		}





		protected string VideoFileNameBase
		{
			get
			{
				return $"{NicoVideo.MakeVideoFileName(NicoVideo.Title, NicoVideo.RawVideoId)}";
			}
		}

		public async Task CancelCacheRequest()
		{
			if (await _Context.CacnelDownloadRequest(this.RawVideoId, Quality))
			{
				await this.CheckCacheStatus();
			}
		}

		public async Task<StorageFile> GetCacheFile()
		{
			return await _Context.VideoSaveFolder.CreateFileAsync(VideoFileName, CreationCollisionOption.OpenIfExists);
		}

		public Task DeletedTeardown()
		{
			return _Context.CacnelDownloadRequest(RawVideoId, this.Quality);
		}



		#region Event Handler

		
		private async void Stream_OnCacheCanceled(string rawVideoId)
		{
			if (RawVideoId == rawVideoId)
			{
				await HohoemaApp.UIDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
				{
					await CheckCacheStatus();
					await SaveProgress();
				});
			}

			RemoveCacheEventHandler();
		}

		private async void Stream_OnCacheProgress(string rawVideoId, NicoVideoQuality quality, uint head, uint length)
		{
			if (rawVideoId == RawVideoId && quality == Quality)
			{
				await HohoemaApp.UIDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					CacheState = NicoVideoCacheState.NowDownloading;
					CacheProgressSize = Progress.BufferedSize();
					OnPropertyChanged(nameof(CacheProgressSize));
				});
			}
		}


		private async void Stream_OnCacheComplete(string rawVideoId)
		{
			await HohoemaApp.UIDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
			{
				await CheckCacheStatus();
			});

			RemoveCacheEventHandler();
		}


		private void AddCacheEventHandler(NicoVideoDownloader downloader)
		{
			downloader.OnCacheProgress += Stream_OnCacheProgress;
			downloader.OnCacheCanceled += Stream_OnCacheCanceled;
			downloader.OnCacheComplete += Stream_OnCacheComplete;

			_NicoVideoDownloader = downloader;
		}

		private void RemoveCacheEventHandler()
		{
			if (_NicoVideoDownloader != null)
			{
				_NicoVideoDownloader.OnCacheProgress -= Stream_OnCacheProgress;
				_NicoVideoDownloader.OnCacheCanceled -= Stream_OnCacheCanceled;
				_NicoVideoDownloader.OnCacheComplete -= Stream_OnCacheComplete;
				_NicoVideoDownloader = null;
			}
		}

		#endregion
	}


	public class LowQualityNicoVideo : DividedQualityNicoVideo
	{
		public LowQualityNicoVideo(NicoVideo nicoVideo, NicoVideoDownloadContext context) 
			: base(NicoVideoQuality.Low, nicoVideo, context)
		{
		}




		public override string VideoFileName
		{
			get
			{
				return VideoFileNameBase + ".low.mp4";
			}
		}

		public override string ProgressFileName
		{
			get
			{
				return NicoVideo.GetProgressFileName(RawVideoId) + ".low.json";
			}
		}


		public override uint VideoSize
		{
			get
			{
				return NicoVideo.SizeLow;
			}
		}

		public override bool IsAvailable
		{
			get
			{
				return !NicoVideo.IsOriginalQualityOnly;
			}
		}



		
		public override bool CanRequestDownload
		{
			get
			{
				// インターネット繋がってるか
				if (!Util.InternetConnection.IsInternet()) { return false; }

				// キャッシュ済みじゃないか
				if (CacheState == NicoVideoCacheState.Cached) { return false; }

				if (NicoVideo.ContentType != Mntone.Nico2.Videos.Thumbnail.MovieType.Mp4)
				{
					return false;
				}

				// オリジナル画質しか存在しない動画
				if (!IsAvailable)
				{
					return false;
				}

				return true;
			}
		}


		


		





	}

	public class OriginalQualityNicoVideo : DividedQualityNicoVideo
	{
		public OriginalQualityNicoVideo(NicoVideo nicoVideo, NicoVideoDownloadContext context)
			: base(NicoVideoQuality.Original, nicoVideo, context)
		{
		}

		public override string VideoFileName
		{
			get
			{
				return VideoFileNameBase + ".mp4";
			}
		}

		public override string ProgressFileName
		{
			get
			{
				return NicoVideo.GetProgressFileName(RawVideoId) + ".json";
			}
		}


		public override uint VideoSize
		{
			get
			{
				return NicoVideo.SizeHigh;
			}
		}

		public override bool IsAvailable
		{
			get
			{
				return true;
			}
		}


		public override bool CanRequestDownload
		{
			get
			{
				// インターネット繋がってるか
				if (!Util.InternetConnection.IsInternet()) { return false; }

				// キャッシュ済みじゃないか
				if (CacheState == NicoVideoCacheState.Cached) { return false; }

				if (NicoVideo.ContentType != Mntone.Nico2.Videos.Thumbnail.MovieType.Mp4)
				{
					return false;
				}

				// 
				if (NicoVideo.IsOriginalQualityOnly)
				{
					return true;
				}

				// オリジナル画質DL可能時間帯か
				if (NicoVideo.NowLowQualityOnly)
				{
					return false;
				}

				return true;
			}
		}
	}
}