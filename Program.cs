using Microsoft.Extensions.Configuration;
using InstagramComments.Services;
using Bogus;


namespace InstagramComments
{
    internal class Program
    {
        //internal static InstagramServices Services = new InstagramServices();
        internal static int minutos = 2;
        internal static Faker FakerData = new("es");
        internal static IConfiguration configuration = new ConfigurationBuilder().AddUserSecrets<InstagramServices>().Build();
        internal static InstagramServices Services = new InstagramServices();
        static async Task Main(string[] args)
        {
            Console.WriteLine("Inicio Sesion.");

            if (Services._InstaApi.IsUserAuthenticated)
            {
                Console.WriteLine("Llamado a publicacion de comentario.");
                await Services.PublishComment();
            }
            else
            {
                Console.WriteLine($"Usuario fallo en login. Inicializando nuevamente.");
                Services = new InstagramServices();
                await Main(args);
            }
            Console.WriteLine($"Espera de {minutos} horas para siguiente llamado.");
            Thread.Sleep(TimeSpan.FromHours(minutos));
            await Main(args);

        }
    }
}