﻿using System.ComponentModel.Composition;
using System.Windows.Controls;
using YouTrackClientVS.Contracts.Interfaces.ViewModels;
using YouTrackClientVS.Contracts.Interfaces.Views;
using YouTrackClientVS.Infrastructure.Extensions;

namespace YouTrackClientVS.UI.Views
{
    /// <summary>
    /// Logique d'interaction pour PendingChangesSectionView.xaml
    /// </summary>
    [Export(typeof(IPendingChangesSectionView))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public partial class PendingChangesSectionView : UserControl, IPendingChangesSectionView
    {
        [ImportingConstructor]
        public PendingChangesSectionView(IPendingChangesSectionViewModel pendingChangesSectionViewModel)
        {
            InitializeComponent();
            DataContext = pendingChangesSectionViewModel;
            pendingChangesSectionViewModel.Initialize();
        }
    }
}
