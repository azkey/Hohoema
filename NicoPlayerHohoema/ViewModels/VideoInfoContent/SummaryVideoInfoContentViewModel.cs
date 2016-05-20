﻿using Mntone.Nico2.Videos.Thumbnail;
using Mntone.Nico2.Videos.WatchAPI;
using NicoPlayerHohoema.Models;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace NicoPlayerHohoema.ViewModels.VideoInfoContent
{
	public class SummaryVideoInfoContentViewModel : MediaInfoViewModel
	{

		public SummaryVideoInfoContentViewModel(ThumbnailResponse thumbnail, Uri descriptionHtmlUri, PageManager pageManager)
		{
			_ThumbnailResponse = thumbnail;
			_PageManager = pageManager;

			UserName = thumbnail.UserName;
			UserIconUrl = thumbnail.UserIconUrl;
			SubmitDate = thumbnail.PostedAt.LocalDateTime;

			//			UserName = response.UserName;

			Title = thumbnail.Title;
			PlayCount = thumbnail.ViewCount;
			CommentCount = thumbnail.CommentCount;
			MylistCount = thumbnail.MylistCount;

			Tags = thumbnail.Tags.Value
				.Select(x => new TagViewModel(x))
				.ToList();

			VideoDescriptionUri = descriptionHtmlUri;
		}

		
		private DelegateCommand<Uri> _ScriptNotifyCommand;
		public DelegateCommand<Uri> ScriptNotifyCommand
		{
			get
			{
				return _ScriptNotifyCommand
					?? (_ScriptNotifyCommand = new DelegateCommand<Uri>((parameter) =>
					{
						System.Diagnostics.Debug.WriteLine($"script notified: {parameter}");

						var path = parameter.AbsoluteUri;
						// is mylist url?
						if (path.StartsWith("https://www.nicovideo.jp/mylist/"))
						{
							var mylistId = parameter.AbsolutePath.Split('/').Last();
							System.Diagnostics.Debug.WriteLine($"open Mylist: {mylistId}");
							_PageManager.OpenPage(HohoemaPageType.Mylist, mylistId);
						}

						// is nico video url?
						if (path.StartsWith("https://www.nicovideo.jp/watch/"))
						{
							var videoId = parameter.AbsolutePath.Split('/').Last();
							System.Diagnostics.Debug.WriteLine($"open Video: {videoId}");
							_PageManager.OpenPage(HohoemaPageType.VideoInfomation, videoId);
						}

					}));
			}
		}



		public string Title { get; private set; }


		public string UserName { get; private set; }
		public Uri UserIconUrl { get; private set; }

		public DateTime SubmitDate { get; private set; }


		public uint PlayCount { get; private set; }

		public uint CommentCount { get; private set; }

		public uint MylistCount { get; private set; }


		private Uri _VideoDesctiptionUri;
		public Uri VideoDescriptionUri
		{
			get { return _VideoDesctiptionUri; }
			set { SetProperty(ref _VideoDesctiptionUri, value); }
		}


		public List<TagViewModel> Tags { get; private set; }

		ThumbnailResponse _ThumbnailResponse;
		PageManager _PageManager;
	}
}
