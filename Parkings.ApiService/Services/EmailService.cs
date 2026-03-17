namespace Parkings.ApiService.Services
{
    public class EmailService(IConfiguration config)
    {
        public async Task SendAsync(string to, string subject, string body)
        {
            var host = config["Email:Host"]!;
            var port = int.Parse(config["Email:Port"] ?? "587");
            var user = config["Email:User"]!;
            var password = config["Email:Password"]!;
            var from = config["Email:From"] ?? user;

            using var client = new System.Net.Mail.SmtpClient(host, port)
            {
                Credentials = new System.Net.NetworkCredential(user, password),
                //EnableSsl = true
            };

            await client.SendMailAsync(new System.Net.Mail.MailMessage(
                from, to, subject, body));
        }

        public string GenerateCode() =>
            Random.Shared.Next(100000, 999999).ToString();
    }
}
