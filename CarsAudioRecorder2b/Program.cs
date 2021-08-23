using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarsAudioRecorder2b
{
    class Program
    {
        static void Main(string[] args)
        {
            NAudio.CoreAudioApi.MMDevice InputDevice = GetMMDevice();


            if (InputDevice != null)
            {
                Record(InputDevice);
            }
        }


        static NAudio.CoreAudioApi.MMDevice GetMMDevice()
        {
            // Behringer drivers are required
            // I used "UMC Driver 5.12.0 (for Windows 10)"
            // from https://www.behringer.com/product.html?modelCode=P0BK1



            NAudio.CoreAudioApi.MMDevice InputDevice = null;


            using (NAudio.CoreAudioApi.MMDeviceEnumerator enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            {
                foreach (NAudio.CoreAudioApi.MMDevice device in enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.All))
                {
                    Console.Write("{0}, {1}", device.FriendlyName, device.State);

                    if (InputDevice == null)
                    {
                        if (device.FriendlyName.Contains("BEHRINGER UMC 404HD") && device.FriendlyName.Contains("1-4"))
                        {
                            Console.Write("!!!");
                            InputDevice = device;
                        }
                    }

                    Console.WriteLine();
                }
            }

            return InputDevice;
        }


        public static void Record(NAudio.CoreAudioApi.MMDevice InputDevice)
        {
            using (NAudio.CoreAudioApi.WasapiCapture capture = new NAudio.CoreAudioApi.WasapiCapture(InputDevice))
            {

                NAudio.Wave.BufferedWaveProvider[] bwp = new NAudio.Wave.BufferedWaveProvider[4];

                for (int i = 0; i < bwp.Length; i++)
                {
                    bwp[i] = new NAudio.Wave.BufferedWaveProvider(capture.WaveFormat);
                    bwp[i].ReadFully = false;
                }






                NAudio.Wave.MultiplexingWaveProvider[] mwp = new NAudio.Wave.MultiplexingWaveProvider[4];

                for (int i = 0; i < mwp.Length; i++)
                {
                    mwp[i] = new NAudio.Wave.MultiplexingWaveProvider(new NAudio.Wave.IWaveProvider[] { bwp[i], }, 1);
                    mwp[i].ConnectInputToOutput(i, 0);
                }





                capture.DataAvailable += (object sender, NAudio.Wave.WaveInEventArgs e) =>
                {
                    for (int i = 0; i < bwp.Length; i++)
                    {
                        bwp[i].AddSamples(e.Buffer, 0, e.BytesRecorded);
                    }
                };



                NAudio.Wave.IWavePlayer[] wo = new DiskWavePlayer[4];

                for (int i = 0; i < wo.Length; i++)
                {
                    wo[i] = new DiskWavePlayer($"out{i}.wav");

                    wo[i].Init(mwp[i]);
                    wo[i].Play();
                }



                capture.StartRecording();

                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                    Console.WriteLine("yeah");
                }


                capture.StopRecording();


                for (int i = 0; i < wo.Length; i++)
                {
                    wo[i].Stop();
                    wo[i].Dispose();
                }

            }
        }
    }
}
