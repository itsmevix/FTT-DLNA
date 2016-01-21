using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using NMaier.GetOptNet;
using NMaier.SimpleDlna.Server;
using NMaier.SimpleDlna.Utilities;
using System;
using System.IO;
using System.Net;


namespace dlna
{
    internal class Opcoes : GetOpt 
    {
        private string[] macs = new string[0];
        private string[] ips = new string[0];
        private string[] uas = new string[0];
        public DirectoryInfo[] Diretorios = new DirectoryInfo[] { new DirectoryInfo(@"C:\Midias") };
        public DlnaMediaTypes[] Tipos = new DlnaMediaTypes[] {DlnaMediaTypes.Audio, DlnaMediaTypes.Video, DlnaMediaTypes.Image} ;
        public string[] Visualizacoes = new string[0];
        private int porta = 0;
        public FileInfo ArquivoCache = null;
        public bool Ascendente = true;
        public bool ListaOrdens = false;
        public bool ListaVisualizacoes = false;
        public string Ordem = "title";
        public bool Separacao = false;
        
        public string[] Ips
        {
            get
            {
                return ips;
            }
            set
            {
                foreach(var ip in value)
                {
                    try
                    {
                        IPAddress.Parse(ip);
                    }
                    catch(Exception)
                    {
                        throw new GetOptException(string.Format("IP Inválido"));
                    }
                }
                ips = value;
            }
        }

        public string[] Macs
        {
            get
            {
                return macs;
            }
            set
            {
                foreach(var mac in value)
                {
                    if(!IP.IsAcceptedMAC(mac))
                    {
                        throw new GetOptException(string.Format("MAC Address inválido"));
                    }
                }
                macs = value;
            }
        }

        public int Porta
        {
            get
            {
                return porta;
            }
            set
            {
                if(value != 0 && (value < 1 || value > ushort.MaxValue))
                {
                    throw new GetOptException("O valor da porta precisa 2 e" + ushort.MaxValue);
                }
                porta = value;
            }
        }

        public string[] UserAgents
        {
            get
            {
                return uas;
            }
            set
            {
                foreach(var ua in value)
                {
                    if(string.IsNullOrWhiteSpace(ua))
                    {
                        throw new GetOptException(string.Format("User Agent inválido"));
                    }
                    uas = value;
                }
            }
        }
    }
}
