using System;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows.Markup;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Locator;
using Microsoft.Build.Construction;
using System.Text.Encodings.Web;
using Microsoft.Build.Tasks;

namespace compile
{
    internal class Program
    {
        protected static string GeneratorString = "\\compilecommand.exe";
        protected static VisualStudioInstance Instance {  get; set; }

        internal class BaseConfigForBuild
        {
            public string compiler { get; set; }
            public List<string> files { get; set; }
        }
        internal class CompileCommand
        {
            public string file { get; set; }
            public string command { get; set; }
            public string directory { get; set; }
        }
        static void Main(string[] args)
        {
            string executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            executablePath = System.IO.Path.GetDirectoryName(executablePath);
            GeneratorString = executablePath + GeneratorString;
            //GeneratorString = "C:\\Projects\\opensource\\compile_commands_generator\\generator" + GeneratorString;
            IEnumerable<VisualStudioInstance> instances = MSBuildLocator.QueryVisualStudioInstances();
            VisualStudioInstance instance = instances.FirstOrDefault();
            MSBuildLocator.RegisterInstance(instance);
            Instance = instance;
            FileInfo fInfo = new FileInfo(args[0]);
            StartBuild(fInfo.FullName);
            //StartBuild("C:/Projects/opensource/Windows-driver-samples/audio/simpleaudiosample/Source/Filters/Filters.vcxproj");
            //StartBuild("C:/Projects/opensource/Windows-driver-samples/audio/simpleaudiosample/SimpleAudioSample.sln");
            //StartBuild("C:\\Projects\\opensource\\test-build\\build\\test.vcxproj");
        }

        static void StartBuild(string buildFile)
        {
            if (File.Exists(buildFile))
            {
                List<string> commands = new List<string>();
                if (buildFile.EndsWith("sln"))
                {
                    SolutionFile sf = SolutionFile.Parse(buildFile);
                    foreach (ProjectInSolution pis in sf.ProjectsInOrder)
                    {
                        if (pis.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
                        {
                            List<string> _commands = BuildProject(pis.AbsolutePath);
                            CleanProject(pis.AbsolutePath);
                            commands.AddRange(_commands);
                        }
                    }
                }
                else
                {
                    commands = BuildProject(buildFile);
                    CleanProject(buildFile);
                }
                string jsonFinal = string.Join(",", commands.ToArray());
                jsonFinal = "[" + jsonFinal + "]";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "compile_commands.json"), jsonFinal);
                //File.WriteAllText(Path.Combine("C:\\tools", "compile_commands.json"), jsonFinal);
            }
        }

        static void CleanProject(string projectFile)
        {
            IDictionary<string, string> gp = new Dictionary<string, string>();
            gp["LangID"] = CultureInfo.CurrentCulture.LCID.ToString();
            gp["UseCacheToolChain"] = "false";
            Project p = new Project(projectFile, gp, null);
            ProjectInstance pi = p.CreateProjectInstance();

            bool r = pi.Build("Clean", null);
        }
        static List<string> BuildProject(string projectFile)
        {
            IDictionary<string, string> gp = new Dictionary<string, string>();
            gp["LangID"] = CultureInfo.CurrentCulture.LCID.ToString();
            gp["DesignTimeBuild"] = "true";
            //gp["BuildProjectReferences"] = "false";
            //gp["SkipCompilerExecution"] = "true";
            //gp["ProvideCommandLineArgs"] = "true";
            //gp["BuildingInsideVisualStudio"] = "true";
            //gp["DriverType"] = "KMDF";
            //gp["Platform"] = "x64";
            //gp["Configuration"] = "Debug";
            gp["UseCacheToolChain"] = "false";
            gp["CLToolExe"] = GeneratorString;
            //gp["ClCompilerPath"] = GeneratorString;
            //Project p = new Project("C:\\Projects\\opensource\\test-msbuild-file\\build\\src\\main.vcxproj", gp, null);
            //Project p = new Project("C:\\Projects\\opensource\\test-build\\build\\test.vcxproj", gp, null);
            Project p = new Project(projectFile, gp, null);
            ProjectInstance pi = p.CreateProjectInstance();
            // this is what we use
            // pi.EvaluateCondition
            //bool r = pi.Build("ClCompile", null);
            //List<CompileCommand> commands = new List<CompileCommand>();
            List<string> commands = new List<string>();
            IDictionary<string, TargetResult> targetOutput = new Dictionary<string, TargetResult>();
            string folder_path = Path.Combine(pi.Directory, ".compile_commands");
            //string folder_path = Path.Combine("C:\\tools", ".compile_commands");
            if (Directory.Exists(folder_path))
            {
                Directory.Delete(folder_path, true);
            }
            System.Console.WriteLine(folder_path);
            Directory.CreateDirectory(folder_path);
            System.Console.WriteLine("Processing " + projectFile);
            List<ProjectItemInstance> items = GetProjectClCompileItems(pi);
            BaseConfigForBuild bcfb = GenerateConfigForBuild(pi, items);
            MakeTheConfigFile(folder_path, bcfb);
            bool r = pi.Build("CustomBuild", null);
            bool r1 = pi.Build(new string[] { "ClCompile" }, null, out targetOutput);
            if (!r1)
            {
                // use the build of CLCompile for the task and  try with more than one cpp file
                // then if not working try by removing some task from it or to see if there are solution for mock build
                System.Console.WriteLine("couldn't build " + projectFile);
            }
            else
            {
                foreach (ProjectItemInstance pis in items)
                {
                    string fileString = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pis.EvaluatedInclude));
                    fileString = Path.Combine(folder_path, fileString + ".json");
                    string compileCommad = GetJsonCompileBase(fileString);
                    commands.Add(compileCommad);
                }
            }
            return commands;
        }

        static BaseConfigForBuild GenerateConfigForBuild(ProjectInstance project, List<ProjectItemInstance> items)
        {
            List<string> files = new List<string>();
            string compilerPath = GetProjectCompilePath(project);
            foreach (ProjectItemInstance pii in items)
            {
                files.Add(pii.EvaluatedInclude);
            }
            BaseConfigForBuild bcfb = new BaseConfigForBuild();
            bcfb.files = files;
            bcfb.compiler = compilerPath;
            return bcfb;
        }

        static bool MakeTheConfigFile(string folderPath, BaseConfigForBuild config)
        {
            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            string jsonFinal = JsonSerializer.Serialize(config, serializeOptions);
            File.WriteAllText(Path.Combine(folderPath, "compiler_path.txt"), config.compiler);
            File.WriteAllText(Path.Combine(folderPath, "files_path.txt"), string.Join("\n", config.files));
            //File.WriteAllText(Path.Combine(folderPath, "config.json"), jsonFinal);
            return true;
        }

        static List<ProjectItemInstance> GetProjectClCompileItems(ProjectInstance pi)
        {
            List<ProjectItemInstance> items = new List<ProjectItemInstance>();
            foreach (ProjectItemInstance pis in pi.Items)
            {
                if (pis.ItemType == "ClCompile")
                {
                    items.Add(pis);
                }
            }
            return items;
        }
        static string GetProjectCompilePath(ProjectInstance project)
        {
            string compilerPath = "";
            foreach (ProjectPropertyInstance bp in project.Properties)
            {
                if (bp.Name.Equals("ClCompilerPath", StringComparison.OrdinalIgnoreCase))
                {
                    compilerPath = bp.EvaluatedValue;
                }
            }
            return compilerPath;
        }

        static CompileCommand FinalizeCompileCommand(CompileCommand compileCommand, ProjectInstance pi)
        {
            string compilerPath = GetProjectCompilePath(pi);
            compileCommand.command = "\"" + compilerPath + "\" " + compileCommand.command;
            return compileCommand;
        }

        static string GetJsonCompileBase(string filename)
        {
            string fileContent = File.ReadAllText(filename);
            //CompileCommand compileCommand = JsonSerializer.Deserialize<CompileCommand>(fileContent);
            return fileContent;
        }
    }
}
