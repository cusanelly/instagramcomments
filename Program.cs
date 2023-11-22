using InstagramApiSharp;
using InstagramApiSharp.API;
using InstagramApiSharp.API.Builder;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Logger;
using InstagramApiSharp.Classes.Models;

using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

using InstagramComments.Settings;
using System.Reflection;
using InstagramApiSharp.Classes.Android.DeviceInfo;
using System.Text;


namespace InstagramComments
{
    internal class Program
    {
        static InstagramServices services = new InstagramServices();
        internal static int minutos = 10;
        static async Task Main(string[] args)
        {
            Console.WriteLine("Inicio Sesion.");

            if (services._InstaApi.IsUserAuthenticated)
            {
                Console.WriteLine("Llamado a publicacion de comentario.");
                await services.PublishComment();
            }
            else
            {
                Console.WriteLine($"Usuario fallo en login. Inicializando nuevamente.");
                services = new InstagramServices();
            }
            Console.WriteLine($"Espera de {minutos} minutos para siguiente llamado.");
            Thread.Sleep(TimeSpan.FromMinutes(minutos));
            await Main(args);

        }


    }

    internal class InstagramServices
    {
        internal IInstaApi _InstaApi;
        internal string nextpageinsta = "";
        internal string useraccount = "";
        internal int count = 0;
        const string stateFile = "state.bin";
        private AndroidDevice device = new AndroidDevice();
        private string sessionstate = "";
        List<string> Users = new();

        // Uso de Visual Stuido User Secrets.
        IConfiguration configuration = new ConfigurationBuilder().AddUserSecrets<InstagramServices>().Build();
        private InstagramSecrets model = new();
        public InstagramServices()
        {
            GetDevice();


            model = configuration.GetSection("InstagramSecrets").Get<InstagramSecrets>();
            var user = new UserSessionData
            {
                UserName = model.Username,
                Password = model.Password                
            };

            _InstaApi = InstaApiBuilder.CreateBuilder()
                .SetUser(user)
                .UseLogger(new DebugLogger(LogLevel.Exceptions))
                .Build();
            _InstaApi.SetDevice(this.device);

            LoadSession();

            string errormessage = "Login exitoso.";
            if (!_InstaApi.IsUserAuthenticated)
            {
                _ = _InstaApi.SendRequestsBeforeLoginAsync().Result;
                Thread.Sleep(5000);
                // login
                Console.WriteLine($"Logging in as {user.UserName}");
                var loginresult = _InstaApi.LoginAsync().Result;
                if (!loginresult.Succeeded)
                {
                    if (loginresult.Info.NeedsChallenge)
                    {
                        _ = ChallengeManage().Result;
                    }
                    else
                    {
                        errormessage = $"unable to login {loginresult.Info.Message}";
                    }
                    Console.WriteLine(errormessage);
                }
                //return;
                _ = _InstaApi.SendRequestsAfterLoginAsync().Result;
                SaveSession();
            }
        }

        private async Task<List<string>> GetRandomAccounts()
        {
            //var accounts = configuration.GetSection("Instagram").GetSection("instagramaccounts").Value.ToArray();

            int randomaccount = 0;
            //if (String.IsNullOrEmpty(useraccount))
            //{
            //   randomaccount = new Random().Next(0, accounts.Length);
            //}
            randomaccount = new Random().Next(0, model.InstagramAccounts.Length);
            int useridrand = new Random().Next(0, 9999999);

            int maxpageload = new Random().Next(0, 50);
            int randskip = new Random().Next(0, maxpageload);
            IResult<InstaUserShortList> result;
            var pagination = PaginationParameters.MaxPagesToLoad(50);
            try
            {
                if (!Users.Any())
                {
                    result = await _InstaApi.UserProcessor.GetUserFollowersAsync(model.InstagramAccounts[randomaccount].ToString(), pagination);

                    //if (String.IsNullOrEmpty(nextpageinsta) && !useraccount.Equals(accounts[randomaccount]))
                    //{
                    //    result = await _InstaApi.UserProcessor.GetUserFollowersAsync(accounts[randomaccount], pagination);
                    //    nextpageinsta = result.Value.NextMaxId;
                    //    useraccount = accounts[randomaccount];
                    //}
                    //else
                    //{
                    //    result = await _InstaApi.UserProcessor.GetUserFollowersAsync(useraccount, pagination.StartFromMaxId(nextpageinsta));
                    //    nextpageinsta = result.Value.NextMaxId;
                    //    count++;
                    //}

                    //if (count == 5)
                    //{
                    //    Console.WriteLine("Reinicio de valores paginacion y cuenta.");
                    //    nextpageinsta = "";
                    //    useraccount = "";
                    //    count = 0;
                    //}

                    Console.WriteLine($"Cuenta de instagram a usar: {model.InstagramAccounts[randomaccount]}");
                    if (result.Succeeded || result.Value != null)
                    {
                        int skipusercount = result.Value.Count / 2;
                        //Users = result.Value.Where(r => r.Pk > useridrand).Select(r => r.UserName).Skip(randskip / 2).Take(2).ToList();
                        if ((maxpageload % 2) == 0)
                        {
                            Console.WriteLine("Descending...");
                            Users = result.Value.OrderByDescending(r => r.UserName).Where(r => !r.IsPrivate).Select(r => r.UserName).Skip(skipusercount).Take(10).ToList();
                        }
                        else
                        {
                            Console.WriteLine("Reverse resultados...");
                            Users = result.Value.Select(r => r.UserName).Reverse().Skip(skipusercount).Take(10).ToList();

                        }
                        //count = Users.Count;
                        //Console.WriteLine("User search count: " + result.Value.Users?.Count);
                        //if (result.Value.Users?.Count > 0)
                        //{
                        //    int usercount = 0;
                        //    do
                        //    {
                        //        Users = result.Value.Users.Where(r => r.IsVerified && r.FollowersCount > 20 && r.FollowersCount < 5000 && !r.HasAnonymousProfilePicture).Select(t => t).Take(3);

                        //    } while (usercount < 3);

                        //}
                    }
                    else
                    {
                        if (result.Info.ResponseType == ResponseType.ChallengeRequired)
                        {
                            Console.WriteLine("Requiere challenge para obtener listado de seguidores. Iniciando procedimiento de challenge...");
                            if (await ChallengeManage())
                            {
                                await GetRandomAccounts();
                            }
                            else
                            {
                                Console.WriteLine("Error en verificacion de codigo. Cerrando sistema.");
                                throw new NotImplementedException();
                            }


                        }
                        else if (result.Info.ResponseType == ResponseType.LoginRequired)
                        {
                            Console.WriteLine("Error de sesion. Iniciando proceso de login...");
                            await LoginAsync(true);
                        }
                        else
                        {
                            Console.WriteLine("Error while searching users: " + result.Info.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en obtencion de seguidores: {ex.Message}");
                //nextpageinsta = "";
                //useraccount = "";
                //count = 0;
            }
            return Users;
        }

        public async Task PublishComment()
        {
            var usernames = await GetRandomAccounts();
            string comment = "";
            if (usernames == null)
            {
                Console.WriteLine("Listado de usuarios es nulo. Saliendo de iteracion.");
                return;
            }
            try
            {
                if (!usernames.Any())
                {
                    Console.WriteLine($"No se encontraron usuarios");
                    return;
                }

                int contador = usernames.Count;
                Console.WriteLine($"Cantidad de usuarios: {contador}");
                for (int i = 0; i < contador; i += 2)
                {
                    comment = (i + 1 >= contador) ? $"@{usernames[i]}, @{Faker.Internet.UserName()}" : $"@{usernames[i]}, @{usernames[i + 1]}";

                    var commentresult = await _InstaApi.CommentProcessor.CommentMediaAsync(model.PostId, comment);
                    Console.WriteLine($"{i} - Mensaje a enviar: {comment} - Resultado: {commentresult.Succeeded}");
                    if (!commentresult.Succeeded)
                    {
                        if (commentresult.Info.ResponseType == ResponseType.LoginRequired)
                        {
                            Console.WriteLine("Error de sesion. Iniciando proceso de login...");
                            if (await LoginAsync(true))
                                await PublishComment();
                        }
                        else if (commentresult.Info.ResponseType == ResponseType.ChallengeRequired)
                        {
                            Console.WriteLine("Error de challenge. Iniciando proceso de challenge...");
                            await ChallengeManage();
                        }
                        else if (commentresult.Info.ResponseType == ResponseType.Spam)
                        {
                            Console.WriteLine($"Envio de mensajes ha sido declarado como spam. Cerrando ciclo. {JsonConvert.SerializeObject(commentresult.Info)}");
                            Program.minutos *= 2;
                            return;
                        }
                        else
                        {
                            Console.WriteLine($"Error envio de comentario: {commentresult.Info.Message}");
                        }
                    }
                    else
                    {
                        Users.RemoveRange(i, i + 1);
                    }
                    Thread.Sleep(5000);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Thread.Sleep(5000);
            Program.minutos = 10;
            //var result = await _InstaApi.LogoutAsync();
            //if (result.Succeeded)
            //{
            //    Console.WriteLine("Cerrando Sesion.");
            //}
            //await _InstaApi.LogoutAsync();
            //var commentResult = await _InstaApi.CommentProcessor.CommentMediaAsync("", "Hi there!");
            //Console.WriteLine(commentResult.Succeeded
            //    ? $"Comment created: {commentResult.Value.Pk}, text: {commentResult.Value.Text}"
            //    : $"Unable to create comment: {commentResult.Info.Message}");
        }

        private async Task<bool> ChallengeManage()
        {
            bool result = false;
            Console.WriteLine("Inicio de Challenge.");
            var challenge = await _InstaApi.GetChallengeRequireVerifyMethodAsync();
            if (challenge.Succeeded)
            {
                //challenge.Value.StepData.PhoneNumber = model.PhoneNumber;
                Console.WriteLine("Iniciando proceso de petiicon de codigo por correo...");
                var requestforcode = await _InstaApi.RequestVerifyCodeToEmailForChallengeRequireAsync();
                if (requestforcode.Succeeded)
                {
                    Console.WriteLine("Codigo enviado. Revisar correo para insertar codigo.");
                    var code = Console.ReadLine();
                    var codeverification = await _InstaApi.VerifyCodeForChallengeRequireAsync(code);
                    result = codeverification.Succeeded;
                    if (!codeverification.Succeeded)
                    {
                        Console.WriteLine($"unable to login {codeverification.Info.Message}");
                    }
                    else
                    {
                        Console.WriteLine("Codigo aceptado.");
                    }
                }
            }else
            {
                Console.WriteLine($"Error en verificacion de metodo de challenge: {JsonConvert.SerializeObject(challenge.Info)}");
            }
            return result;
        }

        internal async Task<bool> LoginAsync(bool loginask = false)
        {
            bool result = false;
            model = configuration.GetSection("InstagramSecrets").Get<InstagramSecrets>();
            var user = new UserSessionData
            {
                UserName = model.Username,
                Password = model.Password,
                //PublicKey = "33efa3bcdbb00691fc622d8eb4f50938",
                //PublicKeyId = "734750815240333"
            };

            _InstaApi = InstaApiBuilder.CreateBuilder()
                .SetUser(user)
                .UseLogger(new DebugLogger(LogLevel.Exceptions))
                .Build();
            _InstaApi.SetDevice(this.device);

            if (!loginask)
            {
                LoadSession();
            }

            string errormessage = "Login exitoso.";
            if (!_InstaApi.IsUserAuthenticated)
            {
                await _InstaApi.SendRequestsBeforeLoginAsync();
                Thread.Sleep(5000);
                // login
                Console.WriteLine($"Logging in as {user.UserName}");
                var loginresult = await _InstaApi.LoginAsync();
                if (!loginresult.Succeeded)
                {
                    if (loginresult.Info.NeedsChallenge)
                    {
                        result = await ChallengeManage();
                    }
                    else
                    {
                        errormessage = $"unable to login {loginresult.Info.Message}";
                    }
                    Console.WriteLine(errormessage);
                }
                //return;
                await _InstaApi.SendRequestsAfterLoginAsync();
                result = SaveSession();
            }
            return result;
        }

        internal void LoadSession()
        {
            try
            {
                // in .net core or uwp apps don't use LoadStateDataFromStream
                // use this one:

                Console.WriteLine("Loading state from file");
                if (System.IO.File.Exists(stateFile))
                {
                    using (var stream = File.Open(stateFile, FileMode.Open))
                    {
                        using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                        {
                            var stateAsBase64String = reader.ReadString();
                            var stateAsString = Encoding.UTF8.GetString(Convert.FromBase64String(stateAsBase64String));

                            _InstaApi.LoadStateDataFromString(stateAsString);
                        }
                    }
                }
                // you should pass json string as parameter to this function.


                //// load session file if exists
                //if (File.Exists(stateFile))
                //{
                //    Console.WriteLine("Loading state from file");
                //    using (var fs = File.OpenRead(stateFile))
                //    {
                //        _InstaApi.LoadStateDataFromStream(fs);

                //    }
                //}                
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error en creacion de archivo de mantenimiento de estado {e.Message}");
            }
        }

        internal bool SaveSession()
        {
            bool result = false;
            // save session in file
            //var state = _InstaApi.GetStateDataAsStream();
            //using (var fileStream = File.Create(stateFile))
            //{
            //    state.Seek(0, SeekOrigin.Begin);
            //    state.CopyTo(fileStream);
            //}

            // in .net core or uwp apps don't use GetStateDataAsStream.
            // use this one:
            this.sessionstate = _InstaApi.GetStateDataAsString();
            // this returns you session as json string.

            using (var stream = File.Open(stateFile, FileMode.Create))
            {
                this.sessionstate = _InstaApi.GetStateDataAsString();
                var stateAsBase64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(this.sessionstate));

                using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
                {
                    writer.Write(stateAsBase64String);
                    result = true;
                }
            }
            return result;
        }

        private void GetDevice()
        {
            // this is an custom android device based on Huawei Honor 8 Lite (PRA-LA1) device
            this.device = new AndroidDevice
            {
                // Device name
                AndroidBoardName = "HONOR",
                // Device brand
                DeviceBrand = "HUAWEI",
                // Hardware manufacturer
                HardwareManufacturer = "HUAWEI",
                // Device model
                DeviceModel = "PRA-LA1",
                // Device model identifier
                DeviceModelIdentifier = "PRA-LA1",
                // Firmware brand
                FirmwareBrand = "HWPRA-H",
                // Hardware model
                HardwareModel = "hi6250",
                // Device guid
                DeviceGuid = new Guid("be897499-c663-492e-a125-f4c8d3785ebf"),
                // Phone guid
                PhoneGuid = new Guid("7b72321f-dd9a-425e-b3ee-d4aaf476ec52"),
                // Device id based on Device guid
                DeviceId = ApiRequestMessage.GenerateDeviceIdFromGuid(new Guid("be897499-c663-492e-a125-f4c8d3785ebf")),
                // Resolution
                Resolution = "1080x1812",
                // Dpi
                Dpi = "480dpi",
            };
        }

        private async Task ChallengePhone()
        {
            var phonecodeverification = await _InstaApi.RequestVerifyCodeToSMSForChallengeRequireAsync();
            //var challenge = await _InstaApi.RequestVerifyCodeToSMSForChallengeRequireAsync();
            //if (challenge.Info)
            //{

            //}

            //    Console.WriteLine("Iniciando proceso de petiicon de codigo por sms...");
            //var beginphoneverification = await _InstaApi.SubmitPhoneNumberForChallengeRequireAsync(model.PhoneNumber);
            //if (challenge.Value.SubmitPhoneRequired)
            //{

            //    if (beginphoneverification.Succeeded)
            //    {


            //        if (phonecodeverification.Succeeded)
            //        {
            //            Console.WriteLine("Codigo enviado. Revisar telefono para insertar codigo.");
            //            var code = Console.ReadLine();
            //            var codeverification = await _InstaApi.VerifyCodeForChallengeRequireAsync(code);
            //            if (!codeverification.Succeeded)
            //            {
            //                Console.WriteLine($"Error en peticion de codigo de challenge: {JsonConvert.SerializeObject(codeverification.Info.Message)}");
            //            }
            //        }
            //    }
            //    else
            //    {
            //        Console.WriteLine($"Error en peticion de codigo de challenge: {JsonConvert.SerializeObject(beginphoneverification.Info.Message)}");
            //    }
            //}
            //else
            //{
            //    var phonecodeverification = await _InstaApi.RequestVerifyCodeToSMSForChallengeRequireAsync();
            //    if (phonecodeverification.Succeeded)
            //    {
            //        Console.WriteLine("Codigo enviado. Revisar telefono para insertar codigo.");
            //        var code = Console.ReadLine();
            //        var codeverification = await _InstaApi.VerifyCodeForChallengeRequireAsync(code);
            //        if (!codeverification.Succeeded)
            //        {
            //            Console.WriteLine($"Error en peticion de codigo de challenge: {JsonConvert.SerializeObject(codeverification.Info.Message)}");
            //        }
            //    }
            //}
        }
    }
}

internal class InstaResults
{
    internal List<string> Users { get; set; } = new List<string>();
    internal string NextPage { get; set; } = "";
}

