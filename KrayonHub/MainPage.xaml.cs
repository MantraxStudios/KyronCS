using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.IO.Compression;

namespace KrayonHub;

public partial class MainPage : ContentPage
{
    public class ProjectItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Template { get; set; } = "";
        public string Icon { get; set; } = "";
        public string LastModified { get; set; } = "";
        public int Scenes { get; set; }
        public int Assets { get; set; }
        public string ScenesLabel => $"{Scenes} scenes";
        public string AssetsLabel => $"{Assets} assets";
    }

    public class ProjectInfo
    {
        public string Name { get; set; } = "";
        public string Template { get; set; } = "";
        public string Icon { get; set; } = "";
        public string CreatedDate { get; set; } = "";
        public int Scenes { get; set; }
        public int Assets { get; set; }
    }

    public class AssetItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Size { get; set; } = "";
    }

    public class TemplateItem
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
    }

    private ObservableCollection<ProjectItem> _allProjects;
    private ObservableCollection<ProjectItem> _filteredProjects;
    private ObservableCollection<AssetItem> _allAssets;
    private ObservableCollection<AssetItem> _filteredAssets;
    private ObservableCollection<TemplateItem> _templates;
    private string _deleteTargetId = "";
    private string _currentView = "projects";

    public MainPage()
    {
        InitializeComponent();
        InitializeData();
        BindCollections();
    }

    private void InitializeData()
    {
        _allProjects = new ObservableCollection<ProjectItem>();
        LoadProjectsFromDisk();

        _allAssets = new ObservableCollection<AssetItem>
        {
            new() { Id = "a1", Name = "Player_Sprite.png",  Type = "Sprite",  Size = "24 KB" },
            new() { Id = "a2", Name = "Forest_Tileset.png",  Type = "Tileset", Size = "156 KB" },
            new() { Id = "a3", Name = "MainTheme.ogg",       Type = "Audio",   Size = "2.4 MB" },
            new() { Id = "a4", Name = "Enemy_AI.ks",         Type = "Script",  Size = "8 KB" },
            new() { Id = "a5", Name = "UI_Font.ttf",         Type = "Font",    Size = "89 KB" },
            new() { Id = "a6", Name = "Particle_Fire.kfx",   Type = "Effect",  Size = "12 KB" },
            new() { Id = "a7", Name = "Level_01.kscn",       Type = "Scene",   Size = "340 KB" },
            new() { Id = "a8", Name = "Explosion_SFX.wav",   Type = "Audio",   Size = "1.1 MB" },
        };

        _templates = new ObservableCollection<TemplateItem>
        {
            new() { Name = "Blank 3D",     Icon = "🧊" },
        };

        _filteredProjects = new ObservableCollection<ProjectItem>(_allProjects);
        _filteredAssets = new ObservableCollection<AssetItem>(_allAssets);
    }

    private void LoadProjectsFromDisk()
    {
        string appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string projectsPath = Path.Combine(appDataRoaming, "Proyects");

        if (!Directory.Exists(projectsPath))
        {
            Directory.CreateDirectory(projectsPath);
            return;
        }

        var directories = Directory.GetDirectories(projectsPath);

        foreach (var dir in directories)
        {
            var dirInfo = new DirectoryInfo(dir);
            var projectName = dirInfo.Name;
            string projectJsonPath = Path.Combine(dir, "project.json");

            if (File.Exists(projectJsonPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(projectJsonPath);
                    var projectInfo = JsonSerializer.Deserialize<ProjectInfo>(jsonContent);

                    if (projectInfo != null)
                    {
                        var project = new ProjectItem
                        {
                            Id = $"p{Path.GetFileName(dir).GetHashCode()}",
                            Name = projectInfo.Name,
                            Template = projectInfo.Template,
                            Icon = projectInfo.Icon,
                            LastModified = projectInfo.CreatedDate,
                            Scenes = projectInfo.Scenes,
                            Assets = projectInfo.Assets
                        };

                        _allProjects.Add(project);
                    }
                }
                catch
                {
                    var project = new ProjectItem
                    {
                        Id = $"p{Path.GetFileName(dir).GetHashCode()}",
                        Name = projectName,
                        Template = "UNKNOWN",
                        Icon = "📁",
                        LastModified = dirInfo.LastWriteTime.ToString("yyyy-MM-dd"),
                        Scenes = 0,
                        Assets = 0
                    };

                    _allProjects.Add(project);
                }
            }
            else
            {
                var project = new ProjectItem
                {
                    Id = $"p{Path.GetFileName(dir).GetHashCode()}",
                    Name = projectName,
                    Template = "UNKNOWN",
                    Icon = "📁",
                    LastModified = dirInfo.LastWriteTime.ToString("yyyy-MM-dd"),
                    Scenes = 0,
                    Assets = 0
                };

                _allProjects.Add(project);
            }
        }
    }

    private void BindCollections()
    {
        ProjectsCollection.ItemsSource = _filteredProjects;
        AssetsCollection.ItemsSource = _filteredAssets;
        TemplatesCollection.ItemsSource = _templates;
        UpdateProjectCount();
    }

    private void SwitchView(string view)
    {
        _currentView = view;
        ProjectsView.IsVisible = view == "projects";
        AssetsView.IsVisible = view == "assets";
        NewProjectView.IsVisible = view == "newProject";

        BtnProjects.Style = (Style)Resources[view == "assets" ? "SidebarBtn" : "SidebarBtnActive"];
        BtnAssets.Style = (Style)Resources[view == "assets" ? "SidebarBtnActive" : "SidebarBtn"];

        LblPageTitle.Text = view switch
        {
            "projects" => "My Projects",
            "assets" => "Asset Library",
            "newProject" => "New Project",
            _ => "My Projects"
        };

        BtnPrimaryAction.Text = view == "assets" ? "＋ IMPORT ASSET" : "＋ NEW PROJECT";
        BtnPrimaryAction.IsVisible = view != "newProject";

        SearchEntry.Text = "";
    }

    private void OnNavProjects(object sender, EventArgs e) => SwitchView("projects");
    private void OnNavAssets(object sender, EventArgs e) => SwitchView("assets");
    private void OnBackToProjects(object sender, EventArgs e) => SwitchView("projects");

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var term = e.NewTextValue?.ToLowerInvariant() ?? "";

        if (_currentView == "projects" || _currentView == "newProject")
        {
            _filteredProjects.Clear();
            foreach (var p in _allProjects.Where(p => p.Name.ToLowerInvariant().Contains(term)))
                _filteredProjects.Add(p);
            UpdateProjectCount();
        }
        else
        {
            _filteredAssets.Clear();
            foreach (var a in _allAssets.Where(a => a.Name.ToLowerInvariant().Contains(term)))
                _filteredAssets.Add(a);
        }
    }

    private void UpdateProjectCount()
    {
        LblProjectCount.Text = $"{_filteredProjects.Count} project{(_filteredProjects.Count != 1 ? "s" : "")}";
    }

    private void OnNewProjectClicked(object sender, EventArgs e)
    {
        if (_currentView == "assets")
        {
            DisplayAlert("Import Asset", "Asset import dialog would open here.", "OK");
            return;
        }
        SwitchView("newProject");
    }

    private void OnCreateProject(object sender, EventArgs e)
    {
        var name = EntryProjectName.Text?.Trim();
        var selectedTemplate = TemplatesCollection.SelectedItem as TemplateItem;

        if (string.IsNullOrEmpty(name))
        {
            DisplayAlert("Missing Name", "Please enter a project name.", "OK");
            return;
        }

        if (selectedTemplate == null)
        {
            DisplayAlert("Missing Template", "Please select a template.", "OK");
            return;
        }

        if (!CreateProyect(name, selectedTemplate))
        {
            DisplayAlert("Already Exist Proyect", $"\"{name}\".", "OK");
            return;
        }

        var newProject = new ProjectItem
        {
            Id = $"p{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Name = name,
            Template = selectedTemplate.Name.ToUpper(),
            Icon = selectedTemplate.Icon,
            LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Scenes = 1,
            Assets = 0
        };

        _allProjects.Insert(0, newProject);
        _filteredProjects.Insert(0, newProject);

        EntryProjectName.Text = "";
        TemplatesCollection.SelectedItem = null;

        SwitchView("projects");
        UpdateProjectCount();

        DisplayAlert("Project Created", $"\"{name}\" has been created successfully.", "OK");
    }

    public bool CreateProyect(string ProyectName, TemplateItem template)
    {
        string appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string projectsPath = Path.Combine(appDataRoaming, "Proyects");

        if (!Directory.Exists(projectsPath))
        {
            Directory.CreateDirectory(projectsPath);
        }

        string projectPath = Path.Combine(projectsPath, ProyectName);

        if (!Directory.Exists(projectPath))
        {
            Directory.CreateDirectory(projectPath);

            var projectInfo = new ProjectInfo
            {
                Name = ProyectName,
                Template = template.Name.ToUpper(),
                Icon = template.Icon,
                CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Scenes = 1,
                Assets = 0
            };

            string jsonContent = JsonSerializer.Serialize(projectInfo, new JsonSerializerOptions { WriteIndented = true });
            string jsonPath = Path.Combine(projectPath, "project.json");
            File.WriteAllText(jsonPath, jsonContent);

            string zipPath = @"Data/Content.zip";
            string extractPath = $"{projectPath}/Content/";

            Directory.CreateDirectory(extractPath);

            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);


            return true;
        }
        else
        {
            return false;
        }
    }

    private async void OnOpenProject(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var project = _allProjects.FirstOrDefault(p => p.Id == id);
            if (project != null)
            {
                string appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string projectPath = Path.Combine(appDataRoaming, "Proyects", project.Name);

                if (Directory.Exists(projectPath))
                {
                    await DisplayAlert("Open Project", $"Opening \"{project.Name}\" from:\n{projectPath}", "OK");
                    string carpetaEjecutable = AppContext.BaseDirectory;
                    string exePath = $"{carpetaEjecutable}/Engine/KrayonEditor.exe";

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"\"{projectPath}\"",
                        UseShellExecute = true,           
                        CreateNoWindow = false            
                    };

                    Process.Start(psi);
                }
                else
                {
                    await DisplayAlert("Error", $"Project folder not found:\n{projectPath}", "OK");
                }
            }
        }
    }

    private void OnDeleteProject(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var project = _allProjects.FirstOrDefault(p => p.Id == id);
            if (project != null)
            {
                _deleteTargetId = id;
                LblDeleteMsg.Text = $"Are you sure you want to delete \"{project.Name}\"?\nThis action cannot be undone.";
                DeleteModal.IsVisible = true;
            }
        }
    }

    private void OnCancelDelete(object sender, EventArgs e)
    {
        DeleteModal.IsVisible = false;
        _deleteTargetId = "";
    }

    private void OnConfirmDelete(object sender, EventArgs e)
    {
        var project = _allProjects.FirstOrDefault(p => p.Id == _deleteTargetId);
        if (project != null)
        {
            string appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string projectPath = Path.Combine(appDataRoaming, "Proyects", project.Name);

            if (Directory.Exists(projectPath))
            {
                try
                {
                    Directory.Delete(projectPath, true);
                }
                catch (Exception ex)
                {
                    DisplayAlert("Error", $"Could not delete project folder: {ex.Message}", "OK");
                }
            }

            _allProjects.Remove(project);
            _filteredProjects.Remove(project);
            UpdateProjectCount();
        }

        DeleteModal.IsVisible = false;
        _deleteTargetId = "";
    }

    private void OnTemplateSelected(object sender, SelectionChangedEventArgs e)
    {
    }

    private async void OnPreviewAsset(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var asset = _allAssets.FirstOrDefault(a => a.Id == id);
            if (asset != null)
                await DisplayAlert("Preview", $"Previewing \"{asset.Name}\"", "OK");
        }
    }

    private async void OnDeleteAsset(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            var asset = _allAssets.FirstOrDefault(a => a.Id == id);
            if (asset != null)
            {
                bool confirm = await DisplayAlert("Delete Asset",
                    $"Delete \"{asset.Name}\"? This cannot be undone.", "Delete", "Cancel");

                if (confirm)
                {
                    _allAssets.Remove(asset);
                    _filteredAssets.Remove(asset);
                }
            }
        }
    }

    private void OnLogout(object sender, EventArgs e)
    {
        SessionManager.ClearSession();
        Application.Current.MainPage = new LoginPage();
    }
}