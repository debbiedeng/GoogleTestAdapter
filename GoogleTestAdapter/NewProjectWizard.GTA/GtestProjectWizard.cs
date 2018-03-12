﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;
using NewProjectWizard.GTA.Helpers;
using VSLangProj;

namespace NewProjectWizard.GTA
{
    public class GtestProjectWizard : ProjectWizardBase
    {
        private const string GtestIncludePlaceholder = "$gtestinclude$";
        private const string LinkGtestAsDllPlaceholder = "$link_gtest_as_dll$";

        private readonly List<Project> _projectsUnderTest = new List<Project>();
        private Project _gtestProject;

        public override void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, 
            WizardRunKind runKind, object[] customParams)
        {
            _DTE dte = (_DTE) automationObject;

            var cppProjects = dte.Solution.Projects.Cast<Project>().Where(p => p.IsCppProject()).ToList();

            var gtestProjects = GtestHelper.FindGtestProjects(cppProjects, Logger).ToList();
            _gtestProject = gtestProjects.FirstOrDefault();
            Logger.DebugInfo(_gtestProject != null
                ? $"gtest project found at '{_gtestProject.FullName}'"
                : "no gtest project found");

            if (_gtestProject == null && !ContinueWithoutGtestProject())
            {
                throw new WizardCancelledException();
            }
            cppProjects.RemoveAll(p => gtestProjects.Contains(p));

            using (var wizard = new SinglePageWizard(cppProjects))
            {
                if (wizard.ShowDialog() != DialogResult.OK)
                {
                    throw new WizardCancelledException();
                }

                _projectsUnderTest.AddRange(wizard.SelectedProjects);
                Logger.DebugInfo($"Projects under test: {string.Join(", ", _projectsUnderTest.Select(p => p.Name))}");
            }

            string value = GetPlatformToolset(_projectsUnderTest);
            replacementsDictionary.Add(ToolsetPlaceholder, value);
            Logger.DebugInfo($"Platform toolset: '{value}'");

            value = GtestHelper.GetLinkGtestAsDll(_gtestProject);
            replacementsDictionary.Add(LinkGtestAsDllPlaceholder, value);
            Logger.DebugInfo($"Link gtest as DLL: '{value}'");

            value = GtestHelper.GetGtestInclude(_gtestProject);
            replacementsDictionary.Add(GtestIncludePlaceholder, value);
            Logger.DebugInfo($"Includes folder: '{value}'");
            
            FillReplacementDirectory(replacementsDictionary);
        }

        public override void ProjectFinishedGenerating(Project project)
        {
            if (_gtestProject != null)
            {
                SafeAddProjectReference(project, _gtestProject);
            }
            foreach (Project projectUnderTest in _projectsUnderTest)
            {
                SafeAddProjectReference(project, projectUnderTest);
            }
        }

        private bool ContinueWithoutGtestProject()
        {
            string message = "No gtest project has been found, and thus this project will need some extra steps before being compilable (e.g., add Google Test dependency via NuGet). "
                             + "Note that you can create a proper Google Test project by first executing project template 'Google Test DLL'. "
                             + Environment.NewLine
                             + Environment.NewLine
                             + "Continue anyways?";
            string title = "No gtest project found";
            return MessageBox.Show(message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
        }

        private void SafeAddProjectReference(Project project, Project referencedProject)
        {
            try
            {
                VSProject vsProj = project.Object as VSProject;
                vsProj?.References.AddProject(referencedProject);
                Logger.DebugInfo($"Project {project.Name}: Added reference to project {referencedProject.Name}");
            }
            catch (Exception ex)
            {
                // Known issue, remove when fixed
                if (!ex.Message.Equals("Error HRESULT E_FAIL has been returned from a call to a COM component."))
                {
                    Logger.LogError($"Exception while adding project {referencedProject.Name} as reference to project {project.Name}. Exception message: {ex.Message}");
                    throw;
                }
            }
        }

        private string GetPlatformToolset(ICollection<Project> projectsUnderTest)
        {
            try
            {
                var platformToolset = GetPlatformToolsetFromProjects(projectsUnderTest);
                if (platformToolset != null)
                {
                    return platformToolset;
                }
            }
            catch
            {
                // fallback to toolset by VS version
            }

            string result = VisualStudioHelper.GetPlatformToolsetFromVisualStudioVersion();
            Logger.DebugInfo($"Toolset from VS version: '{result}'");
            return result;
        }

        private string GetPlatformToolsetFromProjects(ICollection<Project> projectsUnderTest)
        {
            var toolsetsInUse = new HashSet<string>();
            foreach (Project project in projectsUnderTest)
            {
                XmlDocument projectFile = new XmlDocument();
                projectFile.Load(project.FullName);
                var nodes = projectFile.GetElementsByTagName("PlatformToolset");
                foreach (XmlNode node in nodes)
                {
                    toolsetsInUse.Add(node.InnerText);
                }
            }

            var comparer = new ToolsetComparer();
            var orderedToolsetsInUse = toolsetsInUse.OrderByDescending(ts => ts, comparer).ToList();

            Logger.DebugInfo($"Projects considered when searching Toolsets: {string.Join(", ", projectsUnderTest.Select(p => p.Name))}");
            Logger.DebugInfo($"Toolsets found (known to GTA): {string.Join(", ", orderedToolsetsInUse.Select(ts => $"{ts}({comparer.IsKnownToolset(ts)})"))}");

            var result = orderedToolsetsInUse.FirstOrDefault(comparer.IsKnownToolset);
            Logger.DebugInfo(result != null 
                ? $"Toolset selected: '{result}'" 
                : "No toolset found in projects");
            return result;
        }

        private class ToolsetComparer : IComparer<string>
        {
            private const int UnknownToolset = -1;

            public int Compare(string x, string y)
            {
                return GetToolsetIndex(x).CompareTo(GetToolsetIndex(y));
            }

            public bool IsKnownToolset(string toolset)
            {
                return GetToolsetIndex(toolset) != UnknownToolset;
            }

            private int GetToolsetIndex(string toolset)
            {
                switch (toolset)
                {
                    case "v100": return 100;
                    case "v100_xp": return 200;
                    case "v110": return 300;
                    case "v110_xp": return 400;
                    case "v120": return 500;
                    case "v120_xp": return 600;
                    case "v140": return 700;
                    case "v140_xp": return 800;
                    case "v141": return 900;
                    case "v141_xp": return 1000;
                    default: return UnknownToolset;
                }
            }
        }

    }

}