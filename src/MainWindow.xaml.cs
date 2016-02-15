namespace SimGit
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Threading;
    using System.Xml.Serialization;

    using LibGit2Sharp;

    using SimGit.Properties;

    public partial class MainWindow : Window
    {
        private FileSystemWatcher repoWatcher;
        private RepositoryInfo activeRepo;
        private List<RepositoryInfo> addedRepositories;
        
        public MainWindow()
        {
            this.InitializeComponent();

            var lastRepo = Settings.Default.LastRepo;
            if (lastRepo != null && Directory.Exists(lastRepo.Path))
            {
                this.activeRepo = lastRepo;
            }

            //this.activeRepo = new RepositoryInfo { Name = "simgit", Path = @"E:\nevada_scout\simgit" };
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.LoadSettings();

            this.UpdateSidebar();

            this.UpdateChangeListForActiveRepo();
            this.SetupWatcherForActiveRepo();
        }

        private void UpdateSidebar()
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    new ThreadStart(
                        () =>
                        {
                            try
                            {
                                this.UpdateReposListBox();
                            }
                            catch
                            {
                                // TODO -- something else
                                throw;
                            }
                        }));
            }
            else
            {
                this.UpdateReposListBox();
            }
        }

        private void UpdateReposListBox()
        {
            this.RepoList.Items.Clear();

            foreach (var repository in this.addedRepositories)
            {
                this.RepoList.Items.Add(repository);
            }

            this.RepoList.Items.SortDescriptions.Add(new SortDescription(string.Empty, ListSortDirection.Ascending));
        }

        private void UpdateChangeListForActiveRepo()
        {
            if (this.activeRepo == null) return;

            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    new ThreadStart(
                        () =>
                            {
                                try
                                {
                                    using (var repo = new Repository(this.activeRepo.Path))
                                    {
                                        this.UpdateChangesListBox(repo);
                                    }
                                }
                                catch
                                {
                                    // TODO -- something else
                                    throw;
                                }
                            }));
            }
            else
            {
                using (var repo = new Repository(this.activeRepo.Path))
                {
                    this.UpdateChangesListBox(repo);
                }
            }
        }

        private void UpdateChangesListBox(IRepository repo)
        {
            this.ChangeList.Items.Clear();

            foreach (var change in repo.Diff.Compare<TreeChanges>(repo.Head.Tip.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory))
            {
                this.ChangeList.Items.Add(change.Path);
            }

            this.ChangeList.Items.SortDescriptions.Add(new SortDescription(string.Empty, ListSortDirection.Ascending));
        }



        #region Settings

        private void LoadSettings()
        {
            var settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimGit");

            if (!Directory.Exists(settingsFolder))
            {
                Directory.CreateDirectory(settingsFolder);
            }

            var settingsFilePath = Path.Combine(settingsFolder, "settings.xml");

            var serialiser = new XmlSerializer(typeof(List<RepositoryInfo>));
            using (var fs = new FileStream(settingsFilePath, FileMode.OpenOrCreate))
            {
                try
                {
                    this.addedRepositories = serialiser.Deserialize(fs) as List<RepositoryInfo>;
                }
                catch (InvalidOperationException)
                {
                    this.addedRepositories = new List<RepositoryInfo>();
                }
            }
        }

        private void SaveSettings()
        {
            var settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimGit");

            if (!Directory.Exists(settingsFolder))
            {
                Directory.CreateDirectory(settingsFolder);
            }

            var settingsFilePath = Path.Combine(settingsFolder, "settings.xml");

            var serialiser = new XmlSerializer(typeof(List<RepositoryInfo>));
            using (var fs = new FileStream(settingsFilePath, FileMode.OpenOrCreate))
            {
                serialiser.Serialize(fs, this.addedRepositories);
            }
        }
        
        #endregion

        #region Filesystem Watcher

        // Watch the repository file path and update the changes list if any file changes are detected
        private void SetupWatcherForActiveRepo()
        {
            if (this.activeRepo == null) return;

            if (this.repoWatcher != null)
            {
                // Disable event raising as setting the watcher to null does not prevent event firing
                this.repoWatcher.EnableRaisingEvents = false;
                this.repoWatcher = null;
            }

            this.repoWatcher = new FileSystemWatcher
                                   {
                                       Path = this.activeRepo.Path,
                                       Filter = "*.*",
                                       NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                                       IncludeSubdirectories = true,
                                       EnableRaisingEvents = true
                                   };

            this.repoWatcher.Changed += this.OnChanged;
            this.repoWatcher.Created += this.OnChanged;
            this.repoWatcher.Deleted += this.OnChanged;
            this.repoWatcher.Renamed += this.OnRenamed;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            this.UpdateChangeListForActiveRepo();
        }
        
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            this.UpdateChangeListForActiveRepo();
        }

        #endregion
    }
}
