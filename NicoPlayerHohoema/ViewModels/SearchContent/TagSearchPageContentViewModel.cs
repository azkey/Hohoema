﻿using NicoPlayerHohoema.Models;
using NicoPlayerHohoema.Util;
using NicoPlayerHohoema.Views.Service;
using Prism.Windows.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;

namespace NicoPlayerHohoema.ViewModels
{
	public class TagSearchPageContentViewModel : HohoemaVideoListingPageViewModelBase<VideoInfoControlViewModel>
	{
		public ReactiveProperty<bool> IsFavoriteTag { get; private set; }
		public ReactiveProperty<bool> CanChangeFavoriteTagState { get; private set; }


		public ReactiveCommand AddFavoriteTagCommand { get; private set; }
		public ReactiveCommand RemoveFavoriteTagCommand { get; private set; }


		public ReactiveProperty<bool> FailLoading { get; private set; }

		public SearchOption RequireSearchOption { get; private set; }
		public ReactiveProperty<int> LoadedPage { get; private set; }

		NiconicoContentFinder _ContentFinder;

		public TagSearchPageContentViewModel(HohoemaApp hohoemaApp, PageManager pageManager, MylistRegistrationDialogService mylistDialogService, SearchOption searchOption) 
			: base(hohoemaApp, pageManager, mylistDialogService)
		{
			RequireSearchOption = searchOption;

			_ContentFinder = HohoemaApp.ContentFinder;
			FailLoading = new ReactiveProperty<bool>(false)
				.AddTo(_CompositeDisposable);

			LoadedPage = new ReactiveProperty<int>(1)
				.AddTo(_CompositeDisposable);


			IsFavoriteTag = new ReactiveProperty<bool>(mode: ReactivePropertyMode.DistinctUntilChanged)
				.AddTo(_CompositeDisposable);
			CanChangeFavoriteTagState = new ReactiveProperty<bool>()
				.AddTo(_CompositeDisposable);

			AddFavoriteTagCommand = CanChangeFavoriteTagState
				.ToReactiveCommand()
				.AddTo(_CompositeDisposable);

			RemoveFavoriteTagCommand = IsFavoriteTag
				.ToReactiveCommand()
				.AddTo(_CompositeDisposable);


			IsFavoriteTag.Subscribe(async x =>
			{
				if (_NowProcessFavorite) { return; }

				_NowProcessFavorite = true;

				CanChangeFavoriteTagState.Value = false;
				if (x)
				{
					if (await FavoriteTag())
					{
						Debug.WriteLine(RequireSearchOption.Keyword + "のタグをお気に入り登録しました.");
					}
					else
					{
						// お気に入り登録に失敗した場合は状態を差し戻し
						Debug.WriteLine(RequireSearchOption.Keyword + "のタグをお気に入り登録に失敗");
						IsFavoriteTag.Value = false;
					}
				}
				else
				{
					if (await UnfavoriteTag())
					{
						Debug.WriteLine(RequireSearchOption.Keyword + "のタグをお気に入り解除しました.");
					}
					else
					{
						// お気に入り解除に失敗した場合は状態を差し戻し
						Debug.WriteLine(RequireSearchOption.Keyword + "のタグをお気に入り解除に失敗");
						IsFavoriteTag.Value = true;
					}
				}

				CanChangeFavoriteTagState.Value = IsFavoriteTag.Value == true || HohoemaApp.FavFeedManager.CanMoreAddFavorite(FavoriteItemType.Tag);


				_NowProcessFavorite = false;
			})
			.AddTo(_CompositeDisposable);

		}



		bool _NowProcessFavorite = false;


		public override void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			_NowProcessFavorite = true;

			IsFavoriteTag.Value = false;
			CanChangeFavoriteTagState.Value = false;

			_NowProcessFavorite = false;

			base.OnNavigatedTo(e, viewModelState);
		}


		protected override Task ListPageNavigatedToAsync(CancellationToken cancelToken, NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			if (RequireSearchOption == null) { return Task.CompletedTask; }

			_NowProcessFavorite = true;
			
			// お気に入り登録されているかチェック
			var favManager = HohoemaApp.FavFeedManager;
			IsFavoriteTag.Value = favManager.IsFavoriteItem(FavoriteItemType.Tag, RequireSearchOption.Keyword);
			CanChangeFavoriteTagState.Value = favManager.CanMoreAddFavorite(FavoriteItemType.Tag);
			
			_NowProcessFavorite = false;

			return base.ListPageNavigatedToAsync(cancelToken, e, viewModelState);
		}

		#region Implement HohoemaVideListViewModelBase

		protected override IIncrementalSource<VideoInfoControlViewModel> GenerateIncrementalSource()
		{
			return new VideoSearchSource(RequireSearchOption, HohoemaApp, PageManager);
		}


		protected override void PostResetList()
		{
			var source = IncrementalLoadingItems.Source as VideoSearchSource;
			var searchOption = source.SearchOption;
			var target = searchOption.SearchTarget == SearchTarget.Keyword ? "キーワード" : "タグ";
			var optionText = Util.SortHelper.ToCulturizedText(searchOption.Sort, searchOption.Order);
			UpdateTitle($"{target}検索: {searchOption.Keyword} - {optionText}");
		}

		protected override uint IncrementalLoadCount
		{
			get
			{
				return VideoSearchSource.OneTimeLoadSearchItemCount / 2;
			}
		}

		protected override bool CheckNeedUpdateOnNavigateTo(NavigationMode mode)
		{
			var source = IncrementalLoadingItems.Source as VideoSearchSource;

			if (RequireSearchOption != null)
			{
				return !RequireSearchOption.Equals(source.SearchOption);
			}
			else
			{
				return true;
			}
		}

		#endregion


		private async Task<bool> FavoriteTag()
		{
			var favManager = HohoemaApp.FavFeedManager;
			var result = await favManager.AddFav(FavoriteItemType.Tag, RequireSearchOption.Keyword, RequireSearchOption.Keyword);

			return result == Mntone.Nico2.ContentManageResult.Success || result == Mntone.Nico2.ContentManageResult.Exist;
		}

		private async Task<bool> UnfavoriteTag()
		{
			var favManager = HohoemaApp.FavFeedManager;
			var result = await favManager.RemoveFav(FavoriteItemType.Tag, RequireSearchOption.Keyword);

			return result == Mntone.Nico2.ContentManageResult.Success;
		}
	}
}