using log4net;
using NMaier.GetOptNet;
using NMaier.SimpleDlna.FileMediaServer;
using NMaier.SimpleDlna.Server;
using NMaier.SimpleDlna.Server.Comparers;
using NMaier.SimpleDlna.Server.Views;
using NMaier.SimpleDlna.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace dlna
{
    public static class Program
    {
        private readonly static ManualResetEvent BlockEvent =
    new ManualResetEvent(false);

        private static uint CancelHitCount = 0;

        private static void CancelKeyPressed(object sender,
                                             ConsoleCancelEventArgs e)
        {
            if (CancelHitCount++ == 3)
            {
                LogManager.GetLogger(typeof(Program)).Fatal(
                  "Começando processo de saída de emergência.");
                return;
            }
            e.Cancel = true;
            BlockEvent.Set();
            LogManager.GetLogger(typeof(Program)).Info("Desligamento requerido");
            Console.Title = "DLNA - desligamento ...";
        }

        private static void ListaOrdens()
        {
            var items = from v in ComparerRepository.ListItems()
                        orderby v.Key
                        select v.Value;
            Console.WriteLine("Ordens disponíveis:");
            Console.WriteLine("----------------");
            Console.WriteLine();
            foreach (var i in items)
            {
                Console.WriteLine("  - " + i);
                Console.WriteLine();
            }
        }

        private static void ListaVisualizacoes()
        {
            var items = from v in ViewRepository.ListItems()
                        orderby v.Key
                        select v.Value;
            Console.WriteLine("Visualizações disponíveis:");
            Console.WriteLine("----------------");
            Console.WriteLine();
            foreach (var i in items)
            {
                Console.WriteLine("  - " + i);
                Console.WriteLine();
            }
        }
        static void Main(string[] args)
        {
            Console.WriteLine();
            var opcoes = new Opcoes();
            try
            {
                Console.TreatControlCAsInput = false;
                Console.CancelKeyPress += CancelKeyPressed;

                opcoes.Parse(args);

                if (opcoes.ListaVisualizacoes)
                {
                    ListaVisualizacoes();
                    return;
                }
                if (opcoes.ListaOrdens)
                {
                    ListaOrdens();
                    return;
                }
                if (opcoes.Diretorios.Length == 0)
                {
                    throw new GetOptException("Nenhum diretório especificado");
                }

                var servidor = new HttpServer(opcoes.Porta);
                try
                {

                    using (var autorizador = new HttpAuthorizer(servidor))
                    {
                        if (opcoes.Ips.Length != 0)
                        {
                            autorizador.AddMethod(new IPAddressAuthorizer(opcoes.Ips));
                        }
                        if (opcoes.Macs.Length != 0)
                        {
                            autorizador.AddMethod(new MacAuthorizer(opcoes.Macs));
                        }
                        if (opcoes.UserAgents.Length != 0)
                        {
                            autorizador.AddMethod(
                              new UserAgentAuthorizer(opcoes.UserAgents));
                        }

                        Console.Title = "dlna";

                        var tipos = opcoes.Tipos[0];
                        foreach (var t in opcoes.Tipos)
                        {
                            tipos = tipos | t;
                            servidor.InfoFormat("Tipos ativado {0}", t);
                        }

                        var friendlyName = "dlna";

                        if (opcoes.Separacao)
                        {
                            foreach (var d in opcoes.Diretorios)
                            {
                                servidor.InfoFormat("Montando Servidor de Midia da pasta {0}", d.FullName);
                                var fs = ConfiguraServidorDeArquivos(
                                  opcoes, tipos, new DirectoryInfo[] { d });
                                friendlyName = fs.FriendlyName;
                                servidor.RegisterMediaServer(fs);
                                servidor.NoticeFormat("{0} montado", d.FullName);
                            }
                        }
                        else
                        {
                            servidor.InfoFormat(
                              "Montando Servidor de Midia da pasta {0} ({1})",
                              opcoes.Diretorios[0], opcoes.Diretorios.Length);
                            var fs = ConfiguraServidorDeArquivos(opcoes, tipos, opcoes.Diretorios);
                            friendlyName = fs.FriendlyName;
                            servidor.RegisterMediaServer(fs);
                            servidor.NoticeFormat(
                              "{0} ({1}) montado",
                              opcoes.Diretorios[0], opcoes.Diretorios.Length);
                        }

                        Console.Title = String.Format("{0} - rodando ...", friendlyName);

                        Rodar(servidor);
                    }
                }
                finally
                {
                    servidor.Dispose();
                }

            }
            catch (GetOptException ex)
            {
                Console.Error.WriteLine("Error: {0}\n\n", ex.Message);
                opcoes.PrintUsage();
            }
        }

        private static FileServer ConfiguraServidorDeArquivos(Opcoes opcoes, DlnaMediaTypes tipos, DirectoryInfo[] d)
        {
            var ids = new Identifiers(ComparerRepository.Lookup(opcoes.Ordem), opcoes.Ascendente);

            var fs = new FileServer(tipos, ids, d);
            try
            {
                if (opcoes.ArquivoCache != null)
                {
                    fs.SetCacheFile(opcoes.ArquivoCache);
                }
                fs.Load();
            }
            catch (Exception)
            {
                fs.Dispose();
                throw;
            }
            return fs;
        }
        
        private static void Rodar(HttpServer servidor)
        {
            servidor.Info("CTRL-C para terminar");
            BlockEvent.WaitOne();

            servidor.Info("Desligando!");
            servidor.Info("Fechado!");
        }
    }
}
