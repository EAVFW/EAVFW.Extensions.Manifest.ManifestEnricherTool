using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.GPT
{
    public class GPTReviewPrCommand : Command
    {

        [Alias("--project")]
        [Description("The project path to EAV Model Project")]
        public string Project { get; set; }

        [Alias("--pr")]
        [Description("The project path to EAV Model Project")]
        public string PR { get; set; }


        public GPTReviewPrCommand() : base("pr", "Review a PR with Chat GPT")
        { 
            Handler = COmmandExtensions.Create(this, new Command[0], Run); 
        }
        private async Task<int> Run(ParseResult parseResult, IConsole console)
        {
            var name = Path.GetFileNameWithoutExtension(Project);
            var folder = Path.GetTempPath() + name;
            console.WriteLine(folder);
            if (Directory.Exists(folder))
            {
                setAttributesNormal(new DirectoryInfo(folder));
                Directory.Delete(folder, true);


            }
            Directory.CreateDirectory(folder);
            var repoPath = LibGit2Sharp.Repository.Clone(Project, folder, new CloneOptions {   Checkout = true, FetchOptions = new FetchOptions {  } });
            try
            {
                console.WriteLine(repoPath);
                console.WriteLine(Project);

                using var repo = new Repository(repoPath);

                console.WriteLine("worktrees:");
                console.WriteLine(string.Join("\n", repo.Worktrees.Select(c => c.Name)));
               
                console.WriteLine("Refs:");
                console.WriteLine(string.Join("\n", repo.Refs.Select(c => c.CanonicalName)));
                console.WriteLine("Branches:");
                console.WriteLine(string.Join("\n", repo.Branches.Select(c => c.CanonicalName)));

                string mergeIntoReleaseBranch = "refs/remotes/origin/master";
                string branchToBeMerged = "refs/remotes/origin/tst/job-status";
                TreeChanges treeChanges = repo.Diff.Compare<TreeChanges>(repo.Branches[mergeIntoReleaseBranch].Tip.Tree, repo.Branches[branchToBeMerged].Tip.Tree);

                console.WriteLine("Changes:");
                
                Console.WriteLine(treeChanges.Count<TreeEntryChanges>());
                console.WriteLine(string.Join("\n", treeChanges.Select(c => $"{c.Status.ToString()} {c.Path}\n{c.Mode}\n")));
            }
            finally{
                setAttributesNormal(new DirectoryInfo(folder));
                Directory.Delete(folder, true);
            }



            void setAttributesNormal(DirectoryInfo dir)
            {
                foreach (var subDir in dir.GetDirectories())
                    setAttributesNormal(subDir);
                foreach (var file in dir.GetFiles())
                {
                    file.Attributes = FileAttributes.Normal;
                }
            }

            return 0;
        }
    }

    public class GPTReviewCommand : Command
    {
        public GPTReviewCommand(GPTReviewPrCommand review) : base("review", "Chat GPT EAVFW Reviewer")
        { 
            Handler = COmmandExtensions.Create(this, new[]
             {
               review
            }, Run);
            
        }
        private async Task<int> Run(ParseResult parseResult, IConsole console)
        {
            return 0;
        }
    }
    public class GPTCommand : Command
    {
        public GPTCommand(GPTReviewCommand review) : base("gpt", "ChatGPT EAVFW Developer")
        { 
           Handler = COmmandExtensions.Create(this, new[]
            {
               review
            }, Run); 
        }
        private async Task<int> Run(ParseResult parseResult, IConsole console)
        {
            return 0;
        }
        
    }
    public static class GPTExtensions
    {
        public static IServiceCollection AddGPT(this IServiceCollection services)
        {
            services.AddSingleton<Command, GPTCommand>();
             
            AddCommands(typeof(GPTCommand));

            void AddCommands(Type type)
            {
                foreach (var parameter in type.GetConstructors().First().GetParameters())
                {
                    var parameterType = parameter.ParameterType;
                    if (typeof(Command).IsAssignableFrom(parameterType))
                    {
                        services.AddSingleton(parameterType);

                        AddCommands(parameterType);
                    }
                }
            }

            return services;
        }
    }
}
