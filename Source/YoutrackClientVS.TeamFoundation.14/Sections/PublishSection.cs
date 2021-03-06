﻿using Microsoft.TeamFoundation.Controls;
using System;
using System.ComponentModel.Composition;
using YouTrackClientVS.Contracts.Events;
using YouTrackClientVS.Contracts.Interfaces.Services;
using YouTrackClientVS.Contracts.Interfaces.Views;
using YouTrackClientVS.Contracts.Models.GitClientModels;
using YouTrackClientVS.TeamFoundation.TeamFoundation;
using YouTrackClientVS.UI;

namespace YouTrackClientVS.TeamFoundation.Sections
{
    [TeamExplorerSection(Id, TeamExplorerPageIds.GitCommits, 50)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PublishSection : TeamExplorerBaseSection
    {
        private readonly IAppServiceProvider _appServiceProvider;
        private readonly IUserInformationService _userInformationService;
        private readonly IEventAggregatorService _eventAggregator;
        private IDisposable _obs;
        private const string Id = "D15FE6E3-96A4-44F3-9CD4-5CD71A0C1D5A";

        [ImportingConstructor]
        public PublishSection(
            IPublishSectionView view,
            IAppServiceProvider appServiceProvider,
            IYouTrackClientService youTrackClientService,
            IGitWatcher gitWatcher,
            IUserInformationService userInformationService,
            IEventAggregatorService eventAggregator
        ) : base(view)
        {

            _appServiceProvider = appServiceProvider;
            _userInformationService = userInformationService;
            _eventAggregator = eventAggregator;
            Title = $"{Resources.PublishSectionTitle} to {youTrackClientService.Origin}";
            IsVisible = IsGitLocalRepoAndLoggedIn(gitWatcher.ActiveRepo);
            _obs = _eventAggregator.GetEvent<ActiveRepositoryChangedEvent>()
                .Subscribe(x => IsVisible = IsGitLocalRepoAndLoggedIn(x.ActiveRepository));
        }

        private bool IsGitLocalRepoAndLoggedIn(GitRemoteRepository repo)
        {
            return _userInformationService.ConnectionData.IsLoggedIn && repo != null && string.IsNullOrEmpty(repo.CloneUrl);
        }

        public override void Initialize(object sender, SectionInitializeEventArgs e)
        {
            _appServiceProvider.YouTrackServiceProvider = ServiceProvider = e.ServiceProvider;
            base.Initialize(sender, e);
        }

        public override void Dispose()
        {
            base.Dispose();
            _obs.Dispose();
        }
    }
}