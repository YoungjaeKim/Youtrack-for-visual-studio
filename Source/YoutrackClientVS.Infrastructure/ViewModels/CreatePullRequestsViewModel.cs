﻿using log4net;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfControls;
using YouTrackClientVS.Contracts.Events;
using YouTrackClientVS.Contracts.Interfaces;
using YouTrackClientVS.Contracts.Interfaces.Services;
using YouTrackClientVS.Contracts.Interfaces.ViewModels;
using YouTrackClientVS.Contracts.Interfaces.Views;
using YouTrackClientVS.Contracts.Models.GitClientModels;
using YouTrackClientVS.Infrastructure.Extensions;
namespace YouTrackClientVS.Infrastructure.ViewModels
{
    [Export(typeof(ICreatePullRequestsViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CreatePullRequestsViewModel : ViewModelBase, ICreatePullRequestsViewModel
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly IYouTrackClientService _youTrackClientService;
        private readonly IGitService _gitService;
        private readonly IPageNavigationService<IYouTrackIssuesWindow> _pageNavigationService;
        private readonly IEventAggregatorService _eventAggregator;
        private readonly ITreeStructureGenerator _treeStructureGenerator;
        private readonly IVsTools _vsTools;
        private ReactiveCommand _initializeCommand;
        private ReactiveCommand _removeReviewerCommand;
        private bool _isLoading;
        private string _errorMessage;
        private ReactiveCommand _createNewPullRequestCommand;
        private IEnumerable<GitBranch> _branches;
        private GitBranch _sourceBranch;
        private GitBranch _destinationBranch;
        private string _description;
        private string _title;
        private bool _closeSourceBranch;
        private string _message;
        private GitRemoteRepository _currentRepo;
        private ReactiveList<GitUser> _selectedReviewers;
        private GitPullRequest _remotePullRequest;
        private ReactiveCommand _goToDetailsCommand;
        private ReactiveCommand<Unit, Unit> _setPullRequestDataCommand;
        private IDataNotifier _dataNotifier;

        public string PageTitle { get; } = "Create New Pull Request";


        public string ErrorMessage
        {
            get => _errorMessage;
            set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        public IEnumerable<GitBranch> Branches
        {
            get => _branches;
            set => this.RaiseAndSetIfChanged(ref _branches, value);
        }


        [Required]

        public GitBranch SourceBranch
        {
            get => _sourceBranch;
            set => this.RaiseAndSetIfChanged(ref _sourceBranch, value);
        }


        [Required]
        public GitBranch DestinationBranch
        {
            get => _destinationBranch;
            set => this.RaiseAndSetIfChanged(ref _destinationBranch, value);
        }

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        [Required]
        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        [Required]
        public bool CloseSourceBranch
        {
            get => _closeSourceBranch;
            set => this.RaiseAndSetIfChanged(ref _closeSourceBranch, value);
        }




        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }


        public ReactiveList<GitUser> SelectedReviewers
        {
            get => _selectedReviewers;
            set => this.RaiseAndSetIfChanged(ref _selectedReviewers, value);
        }

        public GitPullRequest RemotePullRequest
        {
            get => _remotePullRequest;
            set => this.RaiseAndSetIfChanged(ref _remotePullRequest, value);
        }

        public ISuggestionProvider ReviewersProvider => new SuggestionProvider(Filter);



        public IPullRequestDiffViewModel PullRequestDiffViewModel { get; set; }

        public string ExistingBranchText => RemotePullRequest == null ? null : $"#{RemotePullRequest.Id} {RemotePullRequest.Title} (created {RemotePullRequest.Created})";

        public IEnumerable<ReactiveCommand> ThrowableCommands => new[] { _initializeCommand, _createNewPullRequestCommand, _setPullRequestDataCommand }.Concat(PullRequestDiffViewModel.ThrowableCommands);

        public IEnumerable<ReactiveCommand> LoadingCommands => new[] { _initializeCommand, _createNewPullRequestCommand, _setPullRequestDataCommand };

        public ICommand InitializeCommand => _initializeCommand;
        public ICommand CreateNewPullRequestCommand => _createNewPullRequestCommand;
        public ICommand RemoveReviewerCommand => _removeReviewerCommand;
        public ICommand GoToDetailsCommand => _goToDetailsCommand;

        [ImportingConstructor]
        public CreatePullRequestsViewModel(
            IYouTrackClientService youTrackClientService,
            IGitService gitService,
            IPageNavigationService<IYouTrackIssuesWindow> pageNavigationService,
            IEventAggregatorService eventAggregator,
            ITreeStructureGenerator treeStructureGenerator,
            ICommandsService commandsService,
            IDataNotifier dataNotifier,
            IPullRequestDiffViewModel pullRequestDiffViewModel
        )
        {
            _youTrackClientService = youTrackClientService;
            _gitService = gitService;
            _pageNavigationService = pageNavigationService;
            _eventAggregator = eventAggregator;
            _treeStructureGenerator = treeStructureGenerator;
            _dataNotifier = dataNotifier;

            PullRequestDiffViewModel = pullRequestDiffViewModel;

            CloseSourceBranch = false;
            SelectedReviewers = new ReactiveList<GitUser>();
        }


        protected override IEnumerable<IDisposable> SetupObservables()
        {
            yield return _eventAggregator.GetEvent<ActiveRepositoryChangedEvent>()
                .Select(x => Unit.Default)
                .InvokeCommand(_initializeCommand);

            this.WhenAnyValue(x => x.SourceBranch, x => x.DestinationBranch)
                .Where((x, y) => x.Item1 != null && x.Item2 != null)
                .Select(x => Unit.Default)
                .InvokeCommand(_setPullRequestDataCommand);
        }

        public void InitializeCommands()
        {
            _initializeCommand = ReactiveCommand.CreateFromTask(_ => LoadBranches(), CanLoadPullRequests());
            _removeReviewerCommand = ReactiveCommand.Create<GitUser>(x => SelectedReviewers.Remove(x));
            _createNewPullRequestCommand = ReactiveCommand.CreateFromTask(_ => CreateNewPullRequest(), CanCreatePullRequest());
            _setPullRequestDataCommand = ReactiveCommand.CreateFromTask(_ => SetPullRequestData(), Observable.Return(true));
            _goToDetailsCommand = ReactiveCommand.Create<long>(id => _pageNavigationService.Navigate<IYouTrackIssueDetailView>(id));
        }


        private async Task CreateNewPullRequest()
        {
            var gitPullRequest = new GitPullRequest(Title, Description, SourceBranch, DestinationBranch)
            {
                Id = RemotePullRequest?.Id ?? 0,
                CloseSourceBranch = CloseSourceBranch,
                Reviewers = SelectedReviewers.ToDictionary(x => x, x => true),
                Version = RemotePullRequest?.Version,
            };

            if (RemotePullRequest == null)
                await _youTrackClientService.CreatePullRequest(gitPullRequest);
            else
            {
                await _youTrackClientService.UpdatePullRequest(gitPullRequest);
            }

            _dataNotifier.ShouldUpdate = true;
            _pageNavigationService.NavigateBack(true);
        }

        private async Task LoadBranches()
        {
            _currentRepo = _gitService.GetActiveRepository();

            Branches = (await _youTrackClientService.GetBranches()).OrderBy(x => x.Name).ToList();

            var currentBranch = _currentRepo.Branches.FirstOrDefault(x => x.IsHead) ?? _currentRepo.Branches.FirstOrDefault();

            SourceBranch = Branches.FirstOrDefault(x => x.Name == currentBranch?.TrackedBranchName) ??
                           Branches.FirstOrDefault();
            DestinationBranch = Branches.FirstOrDefault(x => x.IsDefault) ??
                                Branches.FirstOrDefault(x => x.Name != SourceBranch.Name);

            Message = currentBranch != null && string.IsNullOrEmpty(currentBranch.TrackedBranchName)
                ? $"Warning! Your active local branch {currentBranch.Name} is not tracking any remote branches."
                : string.Empty;
        }

        private async Task SetPullRequestData()
        {
            var pullRequest = await _youTrackClientService.GetPullRequestForBranches(SourceBranch.Name, DestinationBranch.Name);
            var commits = (await _youTrackClientService.GetCommitsRange(SourceBranch, DestinationBranch)).ToList();

            PullRequestDiffViewModel.AddCommits(commits);

            await CreateDiffContent(SourceBranch.Target.Hash, DestinationBranch.Target.Hash);

            if (pullRequest != null)
            {
                Title = pullRequest.Title;
                Description = pullRequest.Description;
                SelectedReviewers.Clear();
                foreach (var reviewer in pullRequest.Reviewers.Where(x => x.Key.Username != pullRequest.Author.Username).Select(x => x.Key))
                    SelectedReviewers.Add(reviewer);
            }
            else
            {
                await SetPullRequestDataFromCommits(PullRequestDiffViewModel.Commits);
            }

            RemotePullRequest = pullRequest;
            this.RaisePropertyChanged(nameof(ExistingBranchText));
        }


        private IObservable<bool> CanLoadPullRequests()
        {
            return Observable.Return(true);
        }

        private IObservable<bool> CanCreatePullRequest()
        {
            return ValidationObservable.Select(x => Unit.Default)
                .Merge(Changed.Select(x => Unit.Default))
                .Select(x => CanExecute()).StartWith(CanExecute());
        }

        private bool CanExecute()
        {
            return IsObjectValid() &&
                   !string.IsNullOrEmpty(SourceBranch?.Name) &&
                   !string.IsNullOrEmpty(DestinationBranch?.Name) &&
                   ValidateBranches();
        }

        public bool ValidateBranches()
        {
            return DestinationBranch?.Name != SourceBranch?.Name;
        }

        private IEnumerable Filter(string arg)
        {
            if (arg.Length < 3)
                return Enumerable.Empty<GitUser>();

            try
            {
                var suggestions = _youTrackClientService.GetRepositoryUsers(arg).Result;
                return suggestions.Except(SelectedReviewers, x => x.Username).ToList();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Enumerable.Empty<GitUser>();
            }
        }
        private async Task SetPullRequestDataFromCommits(List<GitCommit> commits)
        {
            Title = SourceBranch.Name;
            Description = string.Join(Environment.NewLine, commits.Select((x) => $"* " + x.Message?.Trim()).Reverse());
            SelectedReviewers.Clear();
            foreach (var defReviewer in await _youTrackClientService.GetDefaultReviewers())
                SelectedReviewers.Add(defReviewer);
        }

        private async Task CreateDiffContent(string fromCommit, string toCommit)
        {
            //TODO DOODS : .
            return;
            ////var fileDiffs = (await _youTrackClientService.GetCommitsDiff(fromCommit, toCommit)).ToList();
            //PullRequestDiffViewModel.AddFileDiffs(fileDiffs);
            //PullRequestDiffViewModel.FromCommit = fromCommit;
            //PullRequestDiffViewModel.ToCommit = toCommit;
        }
    }
}
