﻿using Microsoft.Data.SqlClient;
using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public class SQLApplyCommand : Command
    {
        public Option<string> ClientSecret = new Option<string>("ClientSecret", "");
        public Option<string> ClientId = new Option<string>("ClientId", "");
        public Option<string> Server = new Option<string>("Server", "");
        public Option<string> DatabaseName = new Option<string>("DatabaseName", "");

        public Option<string[]> Replacements = new Option<string[]>("Values");
        public Option<string[]> Files = new Option<string[]>("Files");

        public SQLApplyCommand() : base("apply")
        {
            Server.AddAlias("-S");
            Add(Server);

            ClientSecret.AddAlias("--client-secret");
            Add(ClientSecret);

            ClientId.AddAlias("--client-id");
            Add(ClientId);
            DatabaseName.AddAlias("-d");
            Add(DatabaseName);

            Replacements.AddAlias("-v");
            Add(Replacements);


            Replacements.AllowMultipleArgumentsPerToken = true;

            Files.AddAlias("-i");
            Add(Files);


            Files.AllowMultipleArgumentsPerToken = true;


            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }

        private async Task Run(ParseResult arg1, IConsole arg2)
        {
            var clientid = arg1.GetValueForOption(ClientId);
            var clientSecret = arg1.GetValueForOption(ClientSecret);
            var server = arg1.GetValueForOption(Server);
            var database = arg1.GetValueForOption(DatabaseName);

            var files = arg1.GetValueForOption(Files);
            var _replacements = arg1.GetValueForOption(Replacements);

            // Use your own server, database, app ID, and secret.
            string ConnectionString = $@"Server={server}; Authentication=Active Directory Service Principal;Command Timeout=300; Encrypt=True; Database={database}; User Id={clientid}; Password={clientSecret}";
            //var files = new[] { @"C:\dev\MedlemsCentralen\obj\dbinit\init.sql", @"C:\dev\MedlemsCentralen\obj\dbinit\init-systemadmin.sql" };
            var replacements = _replacements.ToDictionary(k => k.Substring(0, k.IndexOf('=')), v => v.Substring(v.IndexOf('=') + 1));

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
      
                await conn.OpenAsync();
                foreach(var file in files)
                {
                    var cmdText = File.ReadAllText(file);
                    foreach(var r in replacements)
                    {
                        cmdText = cmdText.Replace($"$({r.Key})", r.Value);
                    }


                   

                    foreach (var sql in cmdText.Split("GO"))
                    {
                        using var cmd = conn.CreateCommand();
                      
                        cmd.CommandText = sql.Trim();
                        //  await context.Context.Database.ExecuteSqlRawAsync(sql);

                        if (!string.IsNullOrEmpty(cmd.CommandText))
                        {
                            var r = await cmd.ExecuteNonQueryAsync();
                            Console.WriteLine("Rows changed: " + r);
                        }
                    }




                  
                     
                }
              
            }
        }
    }
}