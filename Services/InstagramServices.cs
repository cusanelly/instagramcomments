using InstagramApiSharp;
using InstagramApiSharp.API;
using InstagramApiSharp.API.Builder;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Logger;
using InstagramApiSharp.Classes.Models;

using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

using InstagramComments.Settings;
using InstagramApiSharp.Classes.Android.DeviceInfo;
using System.Text;
using InstagramApiSharp.Classes.SessionHandlers;


namespace InstagramComments.Services
{
    internal class InstagramServices
    {
        internal IInstaApi _InstaApi;
        internal ISessionHandler _SessionHandler;
        internal string nextpageinsta = "";
        internal string useraccount = "";
        internal int count = 0;
        private const string stateFile = "state.bin";
        private AndroidDevice device = new AndroidDevice();
        private string sessionstate = "";
        List<string> Users = new();

        // Uso de Visual Stuido User Secrets.

        private InstagramSecrets? model = new();
        public InstagramServices()
        {
            GetDevice();


            model = Program.configuration.GetSection("InstagramSecrets").Get<InstagramSecrets>();
            UserSessionData user = new UserSessionData
            {
                UserName = model.Username,
                Password = model.Password
            };
            _SessionHandler = new FileSessionHandler
            {
                FilePath = stateFile,
                InstaApi = _InstaApi
            };


            _InstaApi = InstaApiBuilder.CreateBuilder()
                .SetUser(user)
                .UseLogger(new DebugLogger(LogLevel.Exceptions))
                //.SetSessionHandler(_SessionHandler)
                .Build();
            _InstaApi.SetDevice(device);

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

            //int randomaccount = 0;
            //if (String.IsNullOrEmpty(useraccount))
            //{
            //   randomaccount = new Random().Next(0, accounts.Length);
            //}
            //randomaccount = new Random().Next(0, model.InstagramAccounts.Length);
            string randomaccount = Program.FakerData.PickRandom(model.InstagramAccounts);/*.Random.Number(0,model.InstagramAccounts.Length);*/
            //int useridrand = new Random().Next(0,99999999);
            //int useridrand = Program.FakerData.Random.Number(0, 99999999);
            /*int maxpageload = new Random().Next(0, 50);*/
            int maxpageload = Program.FakerData.Random.Number(0, 20);
            //int randskip = new Random().Next(0, maxpageload);
            //int randskip = Program.FakerData.Random.Number(0, maxpageload);
            IResult<InstaUserShortList> result;
            var pagination = PaginationParameters.MaxPagesToLoad(maxpageload);
            try
            {
                if (!Users.Any())
                {
                    result = await _InstaApi.UserProcessor.GetUserFollowersAsync(randomaccount, pagination);

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

                    Console.WriteLine($"Cuenta de instagram a usar: {randomaccount}");
                    if (result.Succeeded || result.Value != null)
                    {
                        var tre = result.Value.ToArray();
                        Random.Shared.Shuffle(tre);
                        Users = tre.Select(x => x.UserName).Take(10).ToList();

                        #region Old randomizer
                        //int skipusercount = result.Value.Count / 2;
                        ////Users = result.Value.Where(r => r.Pk > useridrand).Select(r => r.UserName).Skip(randskip / 2).Take(2).ToList();
                        //if ((maxpageload % 2) == 0)
                        //{
                        //    Console.WriteLine("Descending...");
                        //    Users = result.Value.OrderByDescending(r => r.UserName).Where(r => !r.IsPrivate).Select(r => r.UserName).Skip(skipusercount).Take(10).ToList();
                        //}
                        //else
                        //{
                        //    Console.WriteLine("Reverse resultados...");
                        //    Users = result.Value.Select(r => r.UserName).Reverse().Skip(skipusercount).Take(10).ToList();

                        //}
                        #endregion

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
                            if (await LoginAsync(true))
                            {
                                Users = await GetRandomAccounts();
                            }
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
                    Console.WriteLine($"No se encontraron usuarios.");
                    return;
                }

                int contador = usernames.Count;
                Console.WriteLine($"Cantidad de usuarios: {contador}");
                for (int i = 0; i < contador; i += 2)
                {
                    comment = i == contador - 1 ? $"@{usernames[0]}, @{Program.FakerData.Internet.UserName()}" : $"@{usernames[0]}, @{usernames[1]}";

                    //Console.WriteLine($"{i} - Mensaje a enviar: {comment} - Resultado:");

                    var commentresult = await _InstaApi.CommentProcessor.CommentMediaAsync(model.JaiberPost, comment);
                    Console.WriteLine($"{i} - Mensaje a enviar: {comment} - Resultado: {commentresult.Succeeded}");
                    if (!commentresult.Succeeded)
                    {
                        switch (commentresult.Info.ResponseType)
                        {
                            case ResponseType.LoginRequired:
                                Console.WriteLine("Error de sesion. Iniciando proceso de login...");
                                if (await LoginAsync(true))
                                    await PublishComment();
                                break;
                            case ResponseType.ChallengeRequired:
                                Console.WriteLine("Error de challenge. Iniciando proceso de challenge...");
                                await ChallengeManage();
                                break;
                            case ResponseType.Spam:
                                Console.WriteLine($"Envio de mensajes ha sido declarado como spam. Cerrando ciclo. {JsonConvert.SerializeObject(commentresult.Info)}");
                                Program.minutos *= 2;
                                break;
                            default:
                                Console.WriteLine($"Error envio de comentario: {JsonConvert.SerializeObject(commentresult.Info)}");
                                Users.RemoveRange(0, 1);
                                usernames.RemoveRange(0, 1);
                                break;
                        }
                    }
                    else
                    {
                        usernames.RemoveRange(0, 1);
                        Users.RemoveRange(0, 1);
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
        }

        private async Task<bool> ChallengeManage()
        {
            bool result = false;
            Console.WriteLine("Inicio de Challenge.");
            var challenge = await _InstaApi.GetChallengeRequireVerifyMethodAsync();
            if (challenge.Succeeded)
            {
                if (challenge.Value.StepData.PhoneNumber == null)
                {
                    challenge.Value.StepData.PhoneNumber = model.PhoneNumber;
                }

                await GetCodeChallengeEmail();
            }
            else
            {
                Console.WriteLine($"Error en verificacion de metodo de challenge: {JsonConvert.SerializeObject(challenge.Info)}");
            }
            return result;
        }

        private async Task GetCodeChallengeEmail()
        {
            Console.WriteLine("Iniciando proceso de petiicon de codigo por correo...");
            var requestforcode = await _InstaApi.RequestVerifyCodeToEmailForChallengeRequireAsync();
            if (requestforcode.Succeeded)
            {
                await CodeVerification();
            }
        }

        internal async Task LogooutAsync()
        {
            await _InstaApi.LogoutAsync();
            File.Delete(stateFile);
        }


        internal async Task<bool> LoginAsync(bool loginask = false)
        {
            bool result = false;
            model = Program.configuration.GetSection("InstagramSecrets").Get<InstagramSecrets>();
            var user = new UserSessionData
            {
                UserName = model.Username,
                Password = model.Password
            };

            _InstaApi = InstaApiBuilder.CreateBuilder()
                .SetUser(user)
                .UseLogger(new DebugLogger(LogLevel.Exceptions))
                .Build();
            _InstaApi.SetDevice(device);

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
                //_SessionHandler.Load();
                bool sesionvalida = File.Exists(stateFile);
                var datedifference = DateTime.Now - File.GetLastWriteTime(stateFile);

                if (sesionvalida && datedifference.Days < 1 && datedifference.Hours < 1)
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

            //_SessionHandler.Save();



            // save session in file
            //var state = _InstaApi.GetStateDataAsStream();
            //using (var fileStream = File.Create(stateFile))
            //{
            //    state.Seek(0, SeekOrigin.Begin);
            //    state.CopyTo(fileStream);
            //}

            // in .net core or uwp apps don't use GetStateDataAsStream.
            // use this one:
            //this.sessionstate = _InstaApi.GetStateDataAsString();
            //// this returns you session as json string.

            using (var stream = File.Open(stateFile, FileMode.Create))
            {
                sessionstate = _InstaApi.GetStateDataAsString();
                var stateAsBase64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(sessionstate));

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
            device = new AndroidDevice
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
            // send verification code to phone number
            var phoneNumber = await _InstaApi.RequestVerifyCodeToSMSForChallengeRequireAsync();
            if (phoneNumber.Succeeded)
            {
                await CodeVerification();
            }
        }

        private async Task<bool> CodeVerification(bool email = true)
        {
            bool result = false;
            Console.Write("Codigo enviado. Ingresa el codigo enviado: ");
            string? code = Console.ReadLine();
            if (string.IsNullOrEmpty(code))
            {
                Console.WriteLine("No puedes ingresar un codigo en blanco.");
                await CodeVerification();
            }

            var codeverification = await _InstaApi.VerifyCodeForChallengeRequireAsync(code);
            result = codeverification.Succeeded;
            if (!result)
            {
                Console.WriteLine($"Error en verificacion de codigo. {JsonConvert.SerializeObject(codeverification.Info)}");
                Console.WriteLine("Iniciando nueva peticion.");
                if (email)
                {
                    await GetCodeChallengeEmail();
                }
            }
            else
            {
                Console.WriteLine("Codigo aceptado.");
            }
            return result;
        }
    }
}

