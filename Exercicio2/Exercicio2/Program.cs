using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Exercicio2
{
    class ChainedThread
    {
        public bool MainNode = false;
        public Thread previous, next;
        
        public void run(object parameter)
        {
            var message = (StringBuilder)parameter;
            while (true)
            {
                try {
                    string old = message.ToString();
                    for (int i = 0; i < message.Length; i++)
                    {
                        if (Char.IsLower(message[i]))
                        {
                            message[i] = char.ToUpper(message[i]);
                            Console.WriteLine("Thread '{0}' mudou: {1} => {2} \n", Thread.CurrentThread.Name, old, message);
                            break;
                        }
                    }

                    if (message.ToString().Any(c => Char.IsLower(c))) //Existe uma letra minuscula
                    {
                        if (Thread.CurrentThread.Name.Equals(this.next.Name))
                            continue;

                        if (next.ThreadState == ThreadState.WaitSleepJoin)      //Se a proxima thread ja foi iniciada acorda 
                            this.next.Interrupt();                              //Interrompe o comando sleep();
                        else if(next.ThreadState == ThreadState.Unstarted)
                            this.next.Start(message);                           //Inicia a proxima thread

                        Thread.Sleep(Timeout.Infinite);                        //pausa a tread;
                    }
                    else
                    {
                        if (previous.ThreadState == ThreadState.WaitSleepJoin)
                            previous.Interrupt();

                        if (next.ThreadState == ThreadState.WaitSleepJoin)
                            next.Interrupt();

                        if (this.MainNode && !Thread.CurrentThread.Name.Equals(this.next.Name))
                        {
                            if(this.previous.IsAlive)
                                this.previous.Join();

                            if (this.next.IsAlive)
                                this.next.Join();

                            Thread.Sleep(200);
                        }

                        Console.WriteLine("Thread '{0}' \"morrendo\"\n", Thread.CurrentThread.Name);
                        break;
                    }
                }
                catch { }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Insira sua string: ");
            StringBuilder message = new StringBuilder(Console.ReadLine());

            Console.Write("Insira o nomero de threads a serem usadas: ");
            int nThreads = int.Parse(Console.ReadLine());
            if (nThreads == 0)
                return;

            Thread tInitial = null, tPrevious = null, tCurrent = null;
            ChainedThread cInitial = null, cPrevious = null;

            cInitial = new ChainedThread() { MainNode = true }; //primeira
            tInitial = new Thread(new ParameterizedThreadStart(cInitial.run));
            tInitial.Name = "1";

            cInitial.next = tInitial;
            cInitial.previous = tInitial;

            cPrevious = cInitial;
            tPrevious = tInitial;

            tCurrent = tInitial;

            for(int i = 2; i <= nThreads; i++)
            {
                var cCurrent = new ChainedThread() { previous = tPrevious };
                tCurrent = new Thread(new ParameterizedThreadStart(cCurrent.run));
                tCurrent.Name = i.ToString();

                cPrevious.next = tCurrent;

                cPrevious = cCurrent;
                tPrevious = tCurrent;
            }

            cPrevious.next = tInitial;
            cInitial.previous = tCurrent;

            tInitial.Start(message);
            tInitial.Join();

            Console.WriteLine("Resultado final: {0}", message);
            Console.ReadLine();
        }
    }
}
