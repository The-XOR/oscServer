using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
#pragma warning disable CS0420

namespace oscServer
{
	public class Semaphore : IDisposable
	{
		private class QueuedWait
		{
			private SortedList<long, EventWaitHandle> listona;
			private readonly ICollection _listona;
			public QueuedWait()
			{
				_listona = listona = new SortedList<long, EventWaitHandle>();
			}

			[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
			public EventWaitHandle AddQueue(long timeStamp) // inserisce un waithandle nella coda di attesa
			{
				EventWaitHandle wh = new EventWaitHandle(false, EventResetMode.ManualReset);
				lock(_listona.SyncRoot)
					listona.Add(timeStamp, wh);
				return wh;
			}

			public void RemoveQueue(long key, EventWaitHandle wh)
			{
				lock(_listona.SyncRoot)
					listona.Remove(key);

				wh.Dispose();
			}

			public void SignalUnlock()
			{
				// l'elemento in cima e' lo piu' anziano
				lock(_listona.SyncRoot)
				{
					if(listona.Any())
						listona.First().Value.Set();
				}
			}

			public void Clear()
			{
				lock(_listona.SyncRoot)
				{
					foreach(KeyValuePair<long, EventWaitHandle> element in listona)
					{
						element.Value.Set();	// libera i thread in attesizzazione
						element.Value.Dispose();
					}
					listona.Clear();
				}
			}
		}

		#region fields & ctor
		private const int SEMA_FREE = -2;
		private const int SEMA_ACQUIRED = -1;
		private int releaseStatus;
		private volatile int semaforone;
		private QueuedWait queuedWait;
		private EventWaitHandle release_unlock;
		private readonly int timeout;
		private readonly string uniqueSemaID;
		private long lockTime;
		private int originalAcquiredID;
		private string originalAcquiredName;
		public bool Debug { get; set; }

		private readonly bool useInAsyncContext;
		public Semaphore(string name, bool acquired = false, bool asyncSemaphore = false) : this(name, 3000, acquired, asyncSemaphore) {}		// 50% in piu' del timeout di default di MCUML

		public Semaphore(string name, int to, bool acquired = false, bool asyncSemaphore = false)
		{
			Debug = false;
			useInAsyncContext = asyncSemaphore;
			lockTime = 0;
			uniqueSemaID = name;
			semaforone = acquired ? SEMA_ACQUIRED : SEMA_FREE;
			releaseStatus = SEMA_ACQUIRED;
			queuedWait = new QueuedWait();
			release_unlock = new EventWaitHandle(false, EventResetMode.AutoReset);
			timeout = to;
		}
		#endregion fields & ctor

		#region acquire/release
		public bool Acquire()
		{
			if(_disposed.IsDisposed)
			{
				if(Debug)
					System.Diagnostics.Debug.WriteLine($"FAIL: {uniqueSemaID}:thread {getThrdName()} wants to acquire a released semaphore!");
				
				return false;
			}

			releaseStatus = SEMA_ACQUIRED;
			originalAcquiredID = Thread.CurrentThread.ManagedThreadId;
			originalAcquiredName = Thread.CurrentThread.Name;
			bool rv = SEMA_FREE == Interlocked.CompareExchange(ref semaforone, SEMA_ACQUIRED, SEMA_FREE);

			if(Debug)
			{
				if(rv)
					System.Diagnostics.Debug.WriteLine($"{uniqueSemaID}:thread {getThrdName()} acquired");
				else
					System.Diagnostics.Debug.WriteLine($"FAIL: {uniqueSemaID}:thread {getThrdName()} cannot acquire ({semaforone}) ");
			}
			return rv;
		}

		public bool IsFree() => Volatile.Read(ref semaforone) == SEMA_FREE;
				
		public bool Release(int long_timeout = 60000)
		{
			if(_disposed.IsDisposed)
			{
				if(Debug)
					System.Diagnostics.Debug.WriteLine($"FAIL: {uniqueSemaID}:thread {getThrdName()} wants to release a released semaphore!");
				
				return false;
			}

			releaseStatus = SEMA_FREE;  // se c'e' un Lock in corso, Unlock rilascera' pure lo semaforo!
			release_unlock.Reset();
			if(Debug )
				System.Diagnostics.Debug.WriteLine($"{uniqueSemaID}:thread {getThrdName()} called release, next unlock will release!");

			bool rv = SEMA_ACQUIRED == Interlocked.CompareExchange(ref semaforone, SEMA_FREE, SEMA_ACQUIRED);
			// se il Release e' fallito vuol dire che c'e' un thread dentro lo Lock(). Appena detto thread chiamera'
			// ReleaseLock, il semaforo verra' rilasciato automaticamente (dato che releaseStatus e' stato messo a FREE)
			
			if(!rv)
			{
				// potrebbe essere che si sta chiamando release una SECONDA volta (il semaforo gia rilasciato fu)
				// in questo caso, Release ritorna false!
				if(Volatile.Read(ref semaforone) == SEMA_FREE)
					return false;    // il semaforo e' gia' stato rilasciato

				// il semaforo non e' stato rilasciato, quindi potrebbe essere in Lock.
				// se pero' LO STESSO LOCK che ha ottenuto il lock chiama -a pene di segugio- Release() prima di unlock,
				// mettiamo una clausola di salvaguardia che il lock puo' comunque effetturare il release. Segnaliamo comunque la cosa alle Autorita' Competenti.
				rv = getCurThrdID() == Interlocked.CompareExchange(ref semaforone, SEMA_FREE, getCurThrdID());
				if(Debug)
					System.Diagnostics.Debug.WriteLineIf(rv, $"WARNING: {uniqueSemaID}:thread {getThrdName()} released locked semaphore!");
				
				if(!rv)
				{
					if(Debug)
						System.Diagnostics.Debug.WriteLine($"WARNING: {uniqueSemaID}: thread {semaforone} kept semafore lock while thread {getCurThrdID()} wanted to lock");
					
					// A questo punto, se rv e' false vuol dire che un altro thread ha effettuato il lock.
					// unlock sblocca ogni volta waitFree; quando waitFree viene segnalato (in realta' e' un PULSE) vuol dire che Unlock e' stato eseguito.
					// Ora possono accadere due cose: 
					// releaseStatus era gia' SEMA_FREE quando e' stato eseguito l'unlock: in questo caso, Release e' terminata
					// releaseStatus era ancora SEMA_ACQUIRED 1 ciclo macchina prima che Release() lo settasse. in questo caso, il semaforo vale ancora SEMA_ACQUIRED
					release_unlock.WaitOne(long_timeout);
					if(Volatile.Read(ref semaforone) == SEMA_FREE)
						return true;    // il semaforo e' gia' stato rilasciato
					else
						rv = SEMA_ACQUIRED == Interlocked.CompareExchange(ref semaforone, SEMA_FREE, SEMA_ACQUIRED);    // oppure era SEMA_ACQUIRED
				}
			}

			if(Debug)
			{				
				if(rv)
					System.Diagnostics.Debug.WriteLine($"{uniqueSemaID}:thread {getThrdName()} released");
				else
					System.Diagnostics.Debug.WriteLine($"FAIL: {uniqueSemaID}:thread {getThrdName()} cannot release ({semaforone})");
			}
			return rv;
		}
		#endregion acquire/release

		#region lock/unlock
		/// <summary>
		/// Ritorna true se il semaforo e' stato bloccato. Durante il Lock, 
		/// il semaforo NON puo' essere rilasciato. Se un altro thread (thread B ) tenta di rilasciarlo, viene fatto
		/// attendere fino a che il thread corrente (thread A ) non chiama ReleaseLock
		/// </summary>
		/// <returns>boolean</returns>
		public bool Lock(int to = 0)
		{
			if(_disposed.IsDisposed)
			{
				if(Debug)
					System.Diagnostics.Debug.WriteLine($"FAIL: {uniqueSemaID}:thread {getThrdName()} wants to lock a released semaphore!");
				
				return false;
			}

			bool rv = SEMA_ACQUIRED == Interlocked.CompareExchange(ref semaforone, getCurThrdID(), SEMA_ACQUIRED);
			// se rv e' true, Lock ha avuto successo (un solo thread puo' eseguire threadlock)
			// in questo caso, ora semaforone contiene il valore del thread che ha eseguito il lock che, con un abile colpo di 
			// genio diventa l'UNICO THREAD CHE PUO' SBLOCCARLO
			
			if(!rv)
			{
				// se Lock non ha avuto successo (un altro thread ha catturato lo semaforo), attendiamo pazienti. Ma solo se il semaforo e' ancora acquisito
				if(Volatile.Read(ref semaforone) != SEMA_FREE)
				{
					long key = getTimeStamp();
					EventWaitHandle wh = queuedWait.AddQueue(key);  // inserisce un waithandle nella coda di attesa
					if(Debug)
						System.Diagnostics.Debug.WriteLine($"{uniqueSemaID}:thread {getThrdName()} in queue for a lock");

					if(wh.WaitOne(to > 0 ? to : timeout)) // oggetto Reset (locked) -> si attende lo sblocco
					{
						rv = SEMA_ACQUIRED == Interlocked.CompareExchange(ref semaforone, getCurThrdID(), SEMA_ACQUIRED);
					}
					queuedWait.RemoveQueue(key, wh);		// rimuove il wait handle dalla coda di attesa, a prescindere che il lock sia avvenuto o no
				}
			}
			if(rv)
				lockTime = getTimeStamp();

			if(Debug)
			{
				if(rv)
					System.Diagnostics.Debug.WriteLine($"{uniqueSemaID}:thread {getThrdName()} locked");
				else
					System.Diagnostics.Debug.WriteLine( $"FAIL: {uniqueSemaID}:thread {getThrdName()} requiring lock ({semaforone})");
			}
			return rv;
		}

		public bool Unlock()
		{
			/* solo il thread che ha eseguito il Lock puo' rilasciarlo, dato che il valore viene confrontato con ManagedThreadId.
			 Non esiste il caso che il thread A chiami Lock e thread B Unlock, perche' lock/unlock devono essere visti come un tuttuno:
			 Thread A -> Lock
			  ....
			  (intanto Thread B chiede il lock: je tocca da aspetta')
			  ---
			  Thread A -> Unlock
			  OK; a questo punto il thread B puo' ottenere il suo bel lock */
			if(_disposed.IsDisposed)
			{
				if(Debug)
					System.Diagnostics.Debug.WriteLine($"FAIL: {uniqueSemaID}:thread {getThrdName()} wants to unlock a released semaphore!");
				
				return false;
			}

			bool rv = getCurThrdID() == Interlocked.CompareExchange(ref semaforone, releaseStatus, getCurThrdID());
			if(rv)
			{
				if(Debug)
				{
					long delta = getTimeStamp() - lockTime;
					System.Diagnostics.Debug.WriteLineIf(delta > 1000, $"WARNING: {uniqueSemaID}:thread {getThrdName()} long time lock ({delta} msec)");
				}

				release_unlock.Set();     // setta PRIMA waitFree; Release ha priorita' sui prossimi Lock(). Si da un impulso a Release
				Thread.Sleep(1);
				queuedWait.SignalUnlock();		// segnala al prossimo thread in attesa di accaparrarsi il lock
			}

			if(Debug)
			{
				if(rv)
				{
					if(semaforone == SEMA_FREE)
						System.Diagnostics.Debug.WriteLine($"{uniqueSemaID}:thread {getThrdName()} unlocked AND RELEASED ({semaforone})");
					else
						System.Diagnostics.Debug.WriteLine($"{uniqueSemaID}:thread {getThrdName()} unlocked ({semaforone})");
				} else
				{
					System.Diagnostics.Debug.WriteLine($"FAIL: {uniqueSemaID}:thread {getThrdName()} FAILED to release a lock (locked by {semaforone})");
					System.Diagnostics.Debugger.Break();
				}
			}
			return rv;
		}
		#endregion lock/unlock

		#region IDisposable Members
		public void Dispose()
		{
			_dispose(true);
			GC.SuppressFinalize(this);
		}

		private SDispose _disposed = new SDispose();
		private void _dispose(bool disposing)
		{
			if(_disposed.CanDispose())
			{
				if(disposing)
				{
					if(Debug && !IsFree())
					{
						System.Diagnostics.Debug.WriteLine($"WARNING: {uniqueSemaID}:thread {getThrdName()} disposing a semafore that is not FREE {semaforone})");
					}
					release_unlock.Close();
					release_unlock = null;
					queuedWait.Clear();
				}
			}
		}

		~Semaphore()
		{
			_dispose(false);
		}
		#endregion

		private long getTimeStamp() => DateTime.Now.Ticks / 10000;
		private string getThrdName() => useInAsyncContext ? $"{originalAcquiredName}({originalAcquiredID}) [as original]" : $"{Thread.CurrentThread.Name}({getCurThrdID()})";
		private int getCurThrdID() => useInAsyncContext ? originalAcquiredID : Thread.CurrentThread.ManagedThreadId;
	}

	public class SDispose
	{
		private volatile int semaforone = 0;

		public SDispose()
		{
			semaforone = 0;
		}

		public bool CanDispose()
		{
			return 0 == Interlocked.CompareExchange(ref semaforone, 1, 0);
		}

		public bool IsDisposed => Volatile.Read(ref semaforone) == 1;
	}
}
