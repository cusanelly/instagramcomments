﻿using InstagramApiSharp;
using InstagramApiSharp.API;
using InstagramApiSharp.API.Builder;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Logger;
using InstagramApiSharp.Classes.Models;

using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

using InstagramComments.Settings;
using System.Reflection;


namespace InstagramComments
{
    internal class Program
    {
        static InstagramServices services = new InstagramServices();        
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
            Console.WriteLine("Espera de 20 minutos para siguiente llamado.");
            Thread.Sleep(TimeSpan.FromMinutes(20));
            await Main(args);

        }


    }

    internal class InstagramServices
    {
        internal IInstaApi _InstaApi;
        internal string nextpageinsta = "";
        internal string useraccount = "";
        internal int count = 0;

        // Uso de Visual Stuido User Secrets.
        IConfiguration configuration = new ConfigurationBuilder().AddUserSecrets<InstagramServices>().Build();
        private InstagramSecrets model = new();
        public InstagramServices()
        {
            
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
            _InstaApi.SetTimeout(TimeSpan.FromSeconds(90));
            const string stateFile = "state.bin";
            try
            {
                // load session file if exists
                if (File.Exists(stateFile))
                {
                    Console.WriteLine("Loading state from file");
                    using (var fs = File.OpenRead(stateFile))
                    {
                        _InstaApi.LoadStateDataFromStream(fs);
                        // in .net core or uwp apps don't use LoadStateDataFromStream
                        // use this one:
                        // _instaApi.LoadStateDataFromString(new StreamReader(fs).ReadToEnd());
                        // you should pass json string as parameter to this function.
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error en creacion de archivo de mantenimiento de estado {e.Message}");
            }

            string errormessage = "Login exitoso.";
            if (!_InstaApi.IsUserAuthenticated)
            {
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
                return;
            }

            // save session in file
            var state = _InstaApi.GetStateDataAsStream();
            // in .net core or uwp apps don't use GetStateDataAsStream.
            // use this one:
            // var state = _instaApi.GetStateDataAsString();
            // this returns you session as json string.
            using (var fileStream = File.Create(stateFile))
            {
                state.Seek(0, SeekOrigin.Begin);
                state.CopyTo(fileStream);
            }

        }

        private async Task<List<string>> GetRandomAccounts()
        {
            List<string> Users = new();


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
                if (result.Succeeded)
                {
                    int skipusercount = result.Value.Count / 2;
                    //Users = result.Value.Where(r => r.Pk > useridrand).Select(r => r.UserName).Skip(randskip / 2).Take(2).ToList();
                    if ((maxpageload % 2) == 0)
                    {
                        Console.WriteLine("Descending...");
                        Users = result.Value.OrderByDescending(r => r.UserName).Where(r => !r.IsPrivate).Select(r => r.UserName).Skip(skipusercount).Take(30).ToList();
                    }
                    else
                    {
                        Console.WriteLine("Reverse resultados...");
                        Users = result.Value.Select(r => r.UserName).Reverse().Skip(skipusercount).Take(20).ToList();

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
                    if (result.Info.NeedsChallenge)
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
                    Console.WriteLine("Error while searching users: " + result.Info.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                nextpageinsta = "";
                useraccount = "";
                count = 0;
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
                int contador = usernames.Count;
                for (int i = 0; i < contador; i += 2)
                {
                    comment = (i == contador) ? $"@{usernames[i]}, @{usernames[i - 1]}" : $"@{usernames[i]}, @{usernames[i + 1]}";

                    var commentresult = await _InstaApi.CommentProcessor.CommentMediaAsync(model.PostId, comment);
                    Console.WriteLine($"Mensaje a enviar: {comment} - Resultado: {commentresult.Succeeded}");
                    if (!commentresult.Succeeded)
                    {
                        if (commentresult.Info.Message == "login_required")
                        {
                            Console.WriteLine("Error de sesion. Iniciando proceso de login...");
                            await Login();
                        }
                        else if (commentresult.Info.Message == "challenge_required") {
                            Console.WriteLine("Error de challenge. Iniciando proceso de challenge...");
                            await ChallengeManage();
                        }
                        else
                        {
                            Console.WriteLine($"Error envio de comentario: {commentresult.Info.Message}");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Thread.Sleep(5000);
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
            if (challenge.Succeeded || challenge.Info.NeedsChallenge)
            {
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
                        Console.WriteLine("Codigo aceptando.");
                    }

                }                
                else
                {
                    Console.WriteLine("Error de envio codigo por correo. Iniciando proceso de petiicon de codigo por sms...");
                    var phonecodeverification = await _InstaApi.RequestVerifyCodeToSMSForChallengeRequireAsync();
                    if (phonecodeverification.Succeeded)
                    {
                        Console.WriteLine("Codigo enviado. Revisar telefono para insertar codigo.");
                        var code = Console.ReadLine();
                        var codeverification = await _InstaApi.VerifyCodeForChallengeRequireAsync(code);
                        result = codeverification.Succeeded;

                    }
                    else
                    {
                        Console.WriteLine($"Error en peticion de codigo de challenge: {JsonConvert.SerializeObject(requestforcode.Info.Message)}");
                    }
                }

            }
            else
            {
                Console.WriteLine($"Error en verificacion de metodo de challenge: {JsonConvert.SerializeObject(challenge.Info)}");
            }
            return result;
        }

        internal async Task Login()
        {
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
            _InstaApi.SetTimeout(TimeSpan.FromSeconds(90));
            const string stateFile = "state.bin";
            try
            {
                // load session file if exists
                if (File.Exists(stateFile))
                {
                    Console.WriteLine("Loading state from file");
                    using (var fs = File.OpenRead(stateFile))
                    {
                        _InstaApi.LoadStateDataFromStream(fs);
                        // in .net core or uwp apps don't use LoadStateDataFromStream
                        // use this one:
                        // _instaApi.LoadStateDataFromString(new StreamReader(fs).ReadToEnd());
                        // you should pass json string as parameter to this function.
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error en creacion de archivo de mantenimiento de estado {e.Message}");
            }

            string errormessage = "Login exitoso.";
            if (!_InstaApi.IsUserAuthenticated)
            {
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
                return;
            }

            // save session in file
            var state = _InstaApi.GetStateDataAsStream();
            // in .net core or uwp apps don't use GetStateDataAsStream.
            // use this one:
            // var state = _instaApi.GetStateDataAsString();
            // this returns you session as json string.
            using (var fileStream = File.Create(stateFile))
            {
                state.Seek(0, SeekOrigin.Begin);
                state.CopyTo(fileStream);
            }
        }
    }
    


}

internal class InstaResults
{
    internal List<string> Users { get; set; } = new List<string>();
    internal string NextPage { get; set; } = "";
}

