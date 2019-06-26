using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid.Helpers.Mail;
using System.Text.RegularExpressions;
using System;

namespace GPWebFunctions.ContactForm
{
    public static class ContactPostMessage
    {               
        [FunctionName("ContactPostMessage")]
        public static async Task<IActionResult> Run( 
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            [SendGrid(ApiKey = "AzureWebJobsSendGridApiKey")] IAsyncCollector<SendGridMessage> messageCollector,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            string name, email, subject, message = "";

            try
            {
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                name = data?.name;
                email = data?.email;
                subject = data?.subject;
                message = data?.message;
            }
            catch(Exception ex)
            {
                log.LogInformation("Error trying to transform JSON payload - " + ex.ToString());
                return new BadRequestObjectResult("Malformed JSON payload");
            }
            
            if (Regex.IsMatch(name, @"^[A-Za-z]{3,}$"))
                return new BadRequestObjectResult("Name may only contain alpha-numeric characters");

            if (Regex.IsMatch(subject, @"^[<>%\$!#^&*+=|/`~]$"))
                return new BadRequestObjectResult("Subject may not contain special characters");

            if (Regex.IsMatch(message, @"^[<>%\$!#^&*+=|/`~]$"))
                return new BadRequestObjectResult("Message may not contain special characters");

            try
            {
                var fromAddr = new System.Net.Mail.MailAddress(email);
                if (fromAddr.Address != email)
                    return new BadRequestObjectResult("E-Mail address is not valid");
            }
            catch
            {
                return new BadRequestObjectResult("E-Mail address is not valid");
            }

            var mail = new SendGridMessage();
            mail.AddTo(Environment.GetEnvironmentVariable("CONTACT_TO_ADDRESS", EnvironmentVariableTarget.Process));
            mail.SetFrom(email, name);
            mail.SetSubject(subject);
            mail.AddContent("text/html", message);

            await messageCollector.AddAsync(mail);

            return new OkResult();
        }
    }
}