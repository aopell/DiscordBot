using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBotNew.Settings
{
    public struct WriteLock : IDisposable
    {
        private bool lockHeld;
        private ReaderWriterLockSlim rwLock;

        public WriteLock(ReaderWriterLockSlim rwLock)
        {
            this.rwLock = rwLock;
            rwLock.EnterWriteLock();
            lockHeld = true;
        }

        public void Dispose()
        {
            if (lockHeld)
            {
                rwLock.ExitWriteLock();
            }
        }
    }
}
