using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace TicketFunction
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function(nameof(Function1))]
        public async Task Run([QueueTrigger("tickethub", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");
            string messageJson = message.MessageText;
            // Deserialize the message into a ticket obj
            var options = new JsonSerializerOptions
            { PropertyNameCaseInsensitive = true };

            var ticket = JsonSerializer.Deserialize<Tickets>(messageJson, options);

            if (ticket == null)
            {
                _logger.LogError("Failed to deserialize");
                return;
            }

            _logger.LogInformation($"Tickets: ConcertId={ticket.ConcertId}, Email={ticket.Email}, Name={ticket.Name}, Phone={ticket.Phone}, Quantity={ticket.Quantity}, CreditCard={ticket.CreditCard?.Substring(12, 4) ?? "null"}, Expiration={ticket.Expiration}, SecurityCode=*****, Address={ticket.Address}, City={ticket.City}, Province={ticket.Province}, PostalCode={ticket.PostalCode}, Country={ticket.Country}");

            // Add the tickets to Azure sql Database
            // get connection string from app settings
            string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("SQL connection string is not set in the environment variables.");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(); // Note the ASYNC

                var query = "INSERT INTO dbo.Tickets (ConcertId, Email, Name, Phone, Quantity, CreditCard, Expiration, SecurityCode, Address, City, Province, PostalCode, Country) VALUES (@ConcertId, @Email, @Name, @Phone, @Quantity, @CreditCard, @Expiration, @SecurityCode, @Address, @City, @Province, @PostalCode, @Country)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ConcertId", ticket.ConcertId);
                    cmd.Parameters.AddWithValue("@Email", ticket.Email);
                    cmd.Parameters.AddWithValue("@Name", ticket.Name);
                    cmd.Parameters.AddWithValue("@Phone", ticket.Phone);
                    cmd.Parameters.AddWithValue("@Quantity", ticket.Quantity);
                    cmd.Parameters.AddWithValue("@CreditCard", ticket.CreditCard);
                    cmd.Parameters.AddWithValue("@Expiration", ticket.Expiration);
                    cmd.Parameters.AddWithValue("@SecurityCode", ticket.SecurityCode);
                    cmd.Parameters.AddWithValue("@Address", ticket.Address);
                    cmd.Parameters.AddWithValue("@City", ticket.City);
                    cmd.Parameters.AddWithValue("@Province", ticket.Province);
                    cmd.Parameters.AddWithValue("@PostalCode", ticket.PostalCode);
                    cmd.Parameters.AddWithValue("@Country", ticket.Country);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
