using Microsoft.Extensions.Configuration;
using InstagramComments.Services;
using Bogus;


namespace InstagramComments
{
    internal class Program
    {
        //internal static InstagramServices Services = new InstagramServices();
        internal static int minutos = 60;
        internal static Faker FakerData = new("es");
        internal static IConfiguration configuration = new ConfigurationBuilder().AddUserSecrets<InstagramServices>().Build();
        internal static InstagramServices Services = new InstagramServices();
        internal static int choicepath = 0;
        static async Task Main(string[] args)
        {
            Console.WriteLine("Inicio Sesion.");
            if (Services._InstaApi.IsUserAuthenticated)
            {
                Console.WriteLine("Escoge una opcion Comentarios: 0 - Asignar likes a cuentas: 1.");
                choicepath = Convert.ToInt32(Console.ReadLine());

                switch (choicepath)
                {
                    case 1:
                        await Services.LikePosts();
                        break;
                    default: // Comentarios
                        Console.WriteLine("Llamado a publicacion de comentario.");
                        await Services.PublishComment();
                        break;
                }                
            }
            else
            {
                Console.WriteLine($"Usuario fallo en login. Inicializando nuevamente.");
                Services = new InstagramServices();
                await Main(args);
            }
            Thread.Sleep(TimeSpan.FromMinutes(1));
            await Services.LogooutAsync();
            //minutos = FakerData.Random.Number(5, 8);
            Console.WriteLine($"Espera de {minutos} minutos para siguiente llamado.");
            Thread.Sleep(TimeSpan.FromMinutes(minutos));            
            await Main(args);

        }
    }
}