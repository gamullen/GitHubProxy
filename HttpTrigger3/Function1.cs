using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;

namespace Gary.Function
{
    // This function acts as a proxy between the Azure portal and GitHub to allow private repos to be exposed.
    public static class HttpTrigger
    {
        [FunctionName("HttpTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string secretName = "GitHubToken";

            log.LogInformation("C# HTTP trigger function processed a request.");
            string code = null;

            string keyVaultURL = "https://keyvaultgary.vault.azure.net/";
            var kvClient = new SecretClient(new Uri(keyVaultURL), new DefaultAzureCredential());
            log.LogInformation("kvClient = " + kvClient.ToString());
            KeyVaultSecret secret = kvClient.GetSecret(secretName);

            string githubURI = null;
            Exception error = null;
            try
            {
                githubURI = req.Query["githuburi"];
                {
                    string githubAccessToken = secret.Value; 

                    string strAuthHeader = "token " + githubAccessToken;
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3.raw");
                    client.DefaultRequestHeaders.Add("Authorization", strAuthHeader);

                    Stream stream = await client.GetStreamAsync(githubURI);
                    StreamReader reader = new StreamReader(stream);
                    code = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                error = e;
                // empty code var will signal error
            }

            ObjectResult result = null;
            if (null != code)
            {
                result = new OkObjectResult(code);
            }
            else
            {
                string errorMessage = null;
                if (null != error)
                {
                    errorMessage = error.Message;
                }

                result = new BadRequestObjectResult(errorMessage + ", URL passed in = " + githubURI);
            }

            return result;
        }
    }
}
