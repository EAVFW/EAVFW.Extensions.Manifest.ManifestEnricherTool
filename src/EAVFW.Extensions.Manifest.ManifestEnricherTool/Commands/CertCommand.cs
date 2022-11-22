using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public class CertCommand : Command
    {
        public CertCommand() : base("certs", "generalte certs files")
        {


            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }

        private async Task Run(ParseResult parseResult, IConsole console)
        {
            var subject = "CN=MCOIDC";
            {
                using var algorithm = RSA.Create(keySizeInBits: 2048);

                var request = new CertificateRequest(subject, algorithm, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, critical: true));

                var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(2));

                // Note: setting the friendly name is not supported on Unix machines (including Linux and macOS). 
                // To ensure an exception is not thrown by the property setter, an OS runtime check is used here. 
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    certificate.FriendlyName = "OpenIddict Server Development Encryption Certificate";
                }

                // Note: CertificateRequest.CreateSelfSigned() doesn't mark the key set associated with the certificate 
                // as "persisted", which eventually prevents X509Store.Add() from correctly storing the private key. 
                // To work around this issue, the certificate payload is manually exported and imported back 
                // into a new X509Certificate2 instance specifying the X509KeyStorageFlags.PersistKeySet flag. 
                var data = certificate.Export(X509ContentType.Pfx, "password");
                File.WriteAllBytes(subject + "Encryption.pfx",data);
            }
            {
                using var algorithm = RSA.Create(keySizeInBits: 2048);

                var request = new CertificateRequest(subject, algorithm, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

                var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(2));

                // Note: setting the friendly name is not supported on Unix machines (including Linux and macOS). 
                // To ensure an exception is not thrown by the property setter, an OS runtime check is used here. 
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    certificate.FriendlyName = "OpenIddict Server Development Signing Certificate";
                }

                // Note: CertificateRequest.CreateSelfSigned() doesn't mark the key set associated with the certificate 
                // as "persisted", which eventually prevents X509Store.Add() from correctly storing the private key. 
                // To work around this issue, the certificate payload is manually exported and imported back 
                // into a new X509Certificate2 instance specifying the X509KeyStorageFlags.PersistKeySet flag. 
                var data = certificate.Export(X509ContentType.Pfx, "password");
                File.WriteAllBytes(subject+"Signing.pfx", data);
            }
            }
    }
}
