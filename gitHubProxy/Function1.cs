// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/*
 MIT License

Copyright (c) 2021 Gary L. Mullen-Schultz

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Mvc;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Azure.Identity;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Functions.Worker;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Gary.Function
{
    // This function acts as a proxy between the Azure portal and GitHub to allow private repos to be exposed for resource creation.
    public class HttpTrigger
    {
        private readonly ILogger<HttpTrigger> log;
        private static string errorMessage = null;

        public HttpTrigger(ILogger<HttpTrigger> logger)
        {
            log = logger;
        }

        [Function("HttpTrigger")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            string secretName = "GitHubToken";

            log.LogInformation("C# HTTP trigger function processed a request.");
            string code = null;

            string keyVaultURL = "https://keyvaultgary.vault.azure.net/";
            var kvClient = new SecretClient(new Uri(keyVaultURL), new DefaultAzureCredential());
            log.LogInformation("kvClient = " + kvClient.ToString());
            KeyVaultSecret secret = null;
            try
            {
                secret = kvClient.GetSecret(secretName);
            } catch (Exception ex)
            {
                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);
            }

            string gitHubURL = null;

            try
            {
                gitHubURL = req.Query["gitHubURL"];

                if (null == validateGitHubURL(gitHubURL))
                {
                    {
                        string githubAccessToken = secret.Value;

                        string strAuthHeader = "token " + githubAccessToken;
                        HttpClient client = new HttpClient();
                        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3.raw");
                        client.DefaultRequestHeaders.Add("Authorization", strAuthHeader);

                        Stream stream = await client.GetStreamAsync(gitHubURL);
                        StreamReader reader = new StreamReader(stream);
                        code = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                log.LogInformation($"Error: {errorMessage}");  
                // empty code var will signal error
            }

            ObjectResult result = null;
            if (null != code)
            {
                result = new OkObjectResult(code);
            }
            else
            {
                result = new BadRequestObjectResult(errorMessage + ", URL passed in = " + gitHubURL);
            }

            return result;
        }

        private static string validateGitHubURL(string url)
        {
            if (null == url)
            {
                errorMessage = "Null gitHubURL parameter.";
            }
            else if (!url.StartsWith("https://raw.githubusercontent.com/"))
            {
                errorMessage = "Invalid gitHubURL parameter, must point to 'https://raw.githubusercontent.com'";
            }

            return errorMessage;
        }
    }
}