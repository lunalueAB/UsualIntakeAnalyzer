using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>사업/기수/차수 CRUD 다이얼로그.</summary>
    public partial class SourceManageDialog : Window
    {
        private enum NodeKind { Project, Phase, Round }
        private class SourceNode
        {
            public NodeKind Kind { get; set; }
            public string   Id   { get; set; } = "";
        }

        public SourceManageDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => BuildTree();
        }

        // ── 트리 빌드 ────────────────────────────────────────────────
        private void BuildTree()
        {
            TreeSources.Items.Clear();

            var projects = SurveySourceService.LoadProjects();
            var phases   = SurveySourceService.LoadPhases();
            var rounds   = SurveySourceService.LoadRounds();
            foreach (var pr in projects.OrderBy(p => p.NameKo))
            {
                var prItem = new TreeViewItem
                {
                    Header     = $"📁 {pr.NameKo}  ({pr.ProjectCode})",
                    Tag        = new SourceNode { Kind = NodeKind.Project, Id = pr.Id },
                    IsExpanded = true,
                    Foreground = (Brush)FindResource("TextBrush")
                };
                foreach (var ph in phases.Where(x => x.ProjectId == pr.Id)
                                          .OrderBy(x => x.PhaseNo))
                {
                    string phYear = (ph.YearStart != null && ph.YearEnd != null)
                        ? (ph.YearStart == ph.YearEnd
                              ? $" · {ph.YearStart}"
                              : $" · {ph.YearStart}–{ph.YearEnd}")
                        : "";
                    var phItem = new TreeViewItem
                    {
                        Header     = $"📂 {ph.PhaseLabel}{phYear}  [{ph.Status}]",
                        Tag        = new SourceNode { Kind = NodeKind.Phase, Id = ph.Id },
                        IsExpanded = true,
                        Foreground = (Brush)FindResource("TextBrush")
                    };
                    foreach (var rd in rounds.Where(x => x.PhaseId == ph.Id)
                                              .OrderBy(x => x.RoundNo))
                    {
                        var rdItem = new TreeViewItem
                        {
                            Header     = $"📄 {rd.DisplayLabel}  [{rd.Status}]",
                            Tag        = new SourceNode { Kind = NodeKind.Round, Id = rd.Id },
                            Foreground = (Brush)FindResource("TextBrush")
                        };
                        phItem.Items.Add(rdItem);
                    }
                    prItem.Items.Add(phItem);
                }
                TreeSources.Items.Add(prItem);
            }
        }

        private SourceNode? GetSelected()
            => (TreeSources.SelectedItem as TreeViewItem)?.Tag as SourceNode;

        private string? ResolveProjectFromSelection()
        {
            var n = GetSelected();
            if (n == null) return null;
            return n.Kind switch
            {
                NodeKind.Project => n.Id,
                NodeKind.Phase   => SurveySourceService.LoadPhases()
                                        .FirstOrDefault(p => p.Id == n.Id)?.ProjectId,
                NodeKind.Round   => SurveySourceService.LoadPhases().FirstOrDefault(p =>
                                        p.Id == SurveySourceService.LoadRounds()
                                            .FirstOrDefault(r => r.Id == n.Id)?.PhaseId)?.ProjectId,
                _                => null
            };
        }

        private string? ResolvePhaseFromSelection()
        {
            var n = GetSelected();
            if (n == null) return null;
            return n.Kind switch
            {
                NodeKind.Phase => n.Id,
                NodeKind.Round => SurveySourceService.LoadRounds()
                                      .FirstOrDefault(r => r.Id == n.Id)?.PhaseId,
                _              => null
            };
        }

        // ── 추가 ────────────────────────────────────────────────────
        private void BtnAddProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ProjectEditDialog(null) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                SurveySourceService.AddProject(dlg.Result);
                BuildTree();
            }
        }

        private void BtnAddPhase_Click(object sender, RoutedEventArgs e)
        {
            string? pid = ResolveProjectFromSelection();
            if (string.IsNullOrEmpty(pid))
            {
                MessageBox.Show("기수를 추가할 사업을 트리에서 선택하세요.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new PhaseEditDialog(null, pid) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                SurveySourceService.AddPhase(dlg.Result);
                BuildTree();
            }
        }

        private void BtnAddRound_Click(object sender, RoutedEventArgs e)
        {
            string? phid = ResolvePhaseFromSelection();
            if (string.IsNullOrEmpty(phid))
            {
                MessageBox.Show("차수를 추가할 기수를 트리에서 선택하세요.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new RoundEditDialog(null, phid) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                SurveySourceService.AddRound(dlg.Result);
                BuildTree();
            }
        }

        // ── 편집 ────────────────────────────────────────────────────
        private void BtnEditNode_Click(object sender, RoutedEventArgs e)
        {
            var n = GetSelected();
            if (n == null) { MessageBox.Show("편집할 항목을 선택하세요."); return; }
            switch (n.Kind)
            {
                case NodeKind.Project:
                {
                    var p = SurveySourceService.LoadProjects()
                        .FirstOrDefault(x => x.Id == n.Id);
                    if (p == null) return;
                    var dlg = new ProjectEditDialog(p) { Owner = this };
                    if (dlg.ShowDialog() == true && dlg.Result != null)
                    {
                        SurveySourceService.UpdateProject(dlg.Result);
                        BuildTree();
                    }
                    break;
                }
                case NodeKind.Phase:
                {
                    var p = SurveySourceService.LoadPhases()
                        .FirstOrDefault(x => x.Id == n.Id);
                    if (p == null) return;
                    var dlg = new PhaseEditDialog(p, p.ProjectId) { Owner = this };
                    if (dlg.ShowDialog() == true && dlg.Result != null)
                    {
                        SurveySourceService.UpdatePhase(dlg.Result);
                        BuildTree();
                    }
                    break;
                }
                case NodeKind.Round:
                {
                    var r = SurveySourceService.LoadRounds()
                        .FirstOrDefault(x => x.Id == n.Id);
                    if (r == null) return;
                    var dlg = new RoundEditDialog(r, r.PhaseId) { Owner = this };
                    if (dlg.ShowDialog() == true && dlg.Result != null)
                    {
                        SurveySourceService.UpdateRound(dlg.Result);
                        BuildTree();
                    }
                    break;
                }
            }
        }

        // ── 삭제 ────────────────────────────────────────────────────
        private void BtnDeleteNode_Click(object sender, RoutedEventArgs e)
        {
            var n = GetSelected();
            if (n == null) { MessageBox.Show("삭제할 항목을 선택하세요."); return; }

            string label = n.Kind switch
            {
                NodeKind.Project => "사업(하위 기수·차수 포함)",
                NodeKind.Phase   => "기수(하위 차수 포함)",
                NodeKind.Round   => "차수",
                _                => "항목"
            };
            if (MessageBox.Show(
                $"이 {label}을(를) 삭제하시겠습니까?\n\n" +
                "* 자료(x0/x1/코드집)는 별도로 남아있을 수 있습니다.",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            switch (n.Kind)
            {
                case NodeKind.Project: SurveySourceService.DeleteProject(n.Id); break;
                case NodeKind.Phase:   SurveySourceService.DeletePhase  (n.Id); break;
                case NodeKind.Round:
                    SurveySourceService.DeleteRound(n.Id);
                    break;
            }
            BuildTree();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
