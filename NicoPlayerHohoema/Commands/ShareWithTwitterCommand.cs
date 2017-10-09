﻿using Prism.Commands;

namespace NicoPlayerHohoema.Commands
{
    public sealed class ShareWithTwitterCommand : DelegateCommandBase
    {
        protected override bool CanExecute(object parameter)
        {
            return parameter is Interfaces.INiconicoContent;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is Interfaces.INiconicoContent)
            {
                var content = parameter as Interfaces.INiconicoContent;

                var shareContent = Models.ShareHelper.MakeShareText(content);
                Models.ShareHelper.ShareToTwitter(shareContent).ConfigureAwait(false);
            }
        }
    }
}
