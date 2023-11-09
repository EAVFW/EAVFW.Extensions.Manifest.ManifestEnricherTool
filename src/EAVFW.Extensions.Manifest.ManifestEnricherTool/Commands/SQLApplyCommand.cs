using Microsoft.Data.SqlClient;
using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    /// <summary>
    /// Example demonstrating creating a custom device code flow authentication provider and attaching it to the driver.
    /// This is helpful for applications that wish to override the Callback for the Device Code Result implemented by the SqlClient driver.
    /// </summary>
    public class AccessTokenProvider : SqlAuthenticationProvider
    {
        private readonly string token;

        public AccessTokenProvider(string token)
        {
            this.token = token;
        }
        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            
            return new SqlAuthenticationToken(token, DateTime.UtcNow.AddHours(1));
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => authenticationMethod.Equals(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal);

        
    }

    public class SQLUserCommand : Command
    {
        public Option<string> ClientSecret = new Option<string>("ClientSecret", "");
        public Option<string> ClientId = new Option<string>("ClientId", "");
        public Option<string> Token = new Option<string>("Token", "");
        public Option<string> Server = new Option<string>("Server", "");
        public Option<string> DatabaseName = new Option<string>("DatabaseName", "");
        public Option<string> ExternalUserName = new Option<string>("ExternalUserName", "");
        public Option<string> ExternalUserObjId = new Option<string>("ExternalUserObjId", "");

        public SQLUserCommand() : base("add-user")
        {
            Server.AddAlias("-S");
            Add(Server);

            ClientSecret.AddAlias("--client-secret");
            Add(ClientSecret);

            ClientId.AddAlias("--client-id");
            Add(ClientId);
            DatabaseName.AddAlias("-d");
            Add(DatabaseName);

             
            Token.AddAlias("--token");
            Add(Token);
            ExternalUserName.AddAlias("--external-user-name");
            Add(ExternalUserName);
            ExternalUserObjId.AddAlias("--external-user-objid");
            Add(ExternalUserObjId);


            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }
        private async Task Run(ParseResult arg1, IConsole arg2)
        {
            var clientid = arg1.GetValueForOption(ClientId);
            var clientSecret = arg1.GetValueForOption(ClientSecret);
            var server = arg1.GetValueForOption(Server);
            var database = arg1.GetValueForOption(DatabaseName);
            var token = arg1.GetValueForOption(Token);
            var user = arg1.GetValueForOption(ExternalUserName);
            var userid = arg1.GetValueForOption(ExternalUserObjId);

            if (!string.IsNullOrEmpty(token))
            {
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, new AccessTokenProvider(token));
            }
            // Use your own server, database, app ID, and secret.
            string ConnectionString = $@"Server={server}; Authentication=Active Directory Service Principal;Command Timeout=300; Encrypt=True; Database={database}; User Id={clientid}; Password={clientSecret}";
            //var files = new[] { @"C:\dev\MedlemsCentralen\obj\dbinit\init.sql", @"C:\dev\MedlemsCentralen\obj\dbinit\init-systemadmin.sql" };
          
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();

                // var sid = "0x";// + string.Format("{0:X2}", Guid.Parse("3438c38d-c721-4373-943c-69b0dfe25462"));
                var sid = "0x";
                foreach (var @byte in Guid.Parse(userid).ToByteArray()) {
                    sid += string.Format("{0:X2}", @byte);
                }

                
                cmd.CommandText = $@" CREATE USER [{user}] WITH DEFAULT_SCHEMA=[dbo], SID = {sid}, TYPE = E;
                                    ALTER ROLE db_datareader ADD MEMBER [{user}];
                                    ALTER ROLE db_datawriter ADD MEMBER [{user}];
                                    ALTER ROLE db_ddladmin ADD MEMBER [{user}];";

                var r = await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("Rows changed: " + r);
            }
        }
            
    }
    public class SQLApplyCommand : Command
    {
        public Option<string> ClientSecret = new Option<string>("ClientSecret", "");
        public Option<string> ClientId = new Option<string>("ClientId", "");
        public Option<string> Token = new Option<string>("Token", "");
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
            Token.AddAlias("--token");
            Add(Token);

            Files.AllowMultipleArgumentsPerToken = true;


            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }

        private async Task Run(ParseResult arg1, IConsole arg2)
        {
            var clientid = arg1.GetValueForOption(ClientId);
            var clientSecret = arg1.GetValueForOption(ClientSecret);
            var server = arg1.GetValueForOption(Server);
            var database = arg1.GetValueForOption(DatabaseName);
            var token = arg1.GetValueForOption(Token);

            var files = arg1.GetValueForOption(Files);
            var _replacements = arg1.GetValueForOption(Replacements);

            if (!string.IsNullOrEmpty(token))
            {
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, new AccessTokenProvider(token));
            }
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
