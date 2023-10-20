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
using Microsoft.Azure.WebJobs.Extensions.Http;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Azure.Identity;

namespace Gary.Function
{
    // This function acts as a proxy between the Azure portal and GitHub to allow private repos to be exposed for resource creation.
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

            string gitHubURL = null;
            Exception error = null;
            try
            {
                gitHubURL = req.Query["gitHubURL"];
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

                result = new BadRequestObjectResult(errorMessage + ", URL passed in = " + gitHubURL);
            }

            return result;
        }
    }
}
