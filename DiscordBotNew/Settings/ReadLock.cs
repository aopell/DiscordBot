using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBotNew.Settings
{
    public struct ReadLock : IDisposable
    {
        private bool lockHeld;
        private ReaderWriterLockSlim rwLock;

        public ReadLock(ReaderWriterLockSlim rwLock)
        {
            this.rwLock = rwLock;
            rwLock.EnterReadLock();
            lockHeld = true;
        }

        public void Dispose()
        {
            if (lockHeld)
            {
                rwLock.ExitReadLock();
            }
        }
    }
}
