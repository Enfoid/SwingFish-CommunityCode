﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StreamDeckSharp.Internals
{
    internal sealed class ConcurrentBufferedQueue<TKey, TValue> : IDisposable
    {
        private readonly object sync = new object();

        private readonly Dictionary<TKey, TValue> valueBuffer = new Dictionary<TKey, TValue>();
        private readonly Dictionary<TKey, long> cooldownValues = new Dictionary<TKey, long>();

        private readonly Queue<TKey> readyQueue = new Queue<TKey>();
        private readonly Queue<TKey> waitingQueue = new Queue<TKey>();

        private readonly ITimeService timeSource;
        private readonly long cooldownTime;
        private volatile bool isAddingCompleted;
        private volatile bool disposed;

        public ConcurrentBufferedQueue(ITimeService timeSource, long cooldown = 100)
        {
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
            cooldownTime = cooldown;
        }

        public int Count
            => readyQueue.Count;

        public bool IsAddingCompleted
        {
            get
            {
                ThrowIfDisposed();
                return isAddingCompleted;
            }
        }

        public bool IsCompleted
        {
            get
            {
                lock (sync)
                {
                    ThrowIfDisposed();
                    return isAddingCompleted && Count == 0;
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock (sync)
            {
                ThrowIfDisposed();

                if (isAddingCompleted)
                {
                    throw new InvalidOperationException("Adding was already marked as completed.");
                }

                try
                {
                    AddOrUpdateBufferDictionary(key, value);

                    if (readyQueue.Contains(key))
                    {
                        return;
                    }

                    if (cooldownValues.ContainsKey(key))
                    {
                        var wasEmpty = waitingQueue.Count < 1;

                        if (!waitingQueue.Contains(key))
                        {
                            waitingQueue.Enqueue(key);
                        }

                        if (wasEmpty)
                        {
                            var timeToNextRelease = cooldownValues[key] - timeSource.GetRelativeTimestamp();

                            if (timeToNextRelease <= 0)
                            {
                                ProcessCooldownList();
                            }
                            else
                            {
                                ProcessCooldownAgainAfterMs(timeToNextRelease);
                            }
                        }

                        return;
                    }

                    readyQueue.Enqueue(key);
                }
                finally
                {
                    Monitor.PulseAll(sync);
                }
            }
        }

        public KeyValuePair<TKey, TValue> Take()
        {
            lock (sync)
            {
                while (readyQueue.Count < 1)
                {
                    ThrowIfDisposed();

                    if (isAddingCompleted)
                    {
                        throw new InvalidOperationException("Adding is completed and buffer is empty.");
                    }

                    Monitor.Wait(sync);
                }

                ThrowIfDisposed();

                var key = readyQueue.Dequeue();
                var value = valueBuffer[key];
                valueBuffer.Remove(key);

                cooldownValues.Add(key, timeSource.GetRelativeTimestamp() + cooldownTime);

                return new KeyValuePair<TKey, TValue>(key, value);
            }
        }

        public void CompleteAdding()
        {
            lock (sync)
            {
                if (isAddingCompleted)
                {
                    return;
                }

                isAddingCompleted = true;
                Monitor.PulseAll(sync);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;

                if (!isAddingCompleted)
                {
                    CompleteAdding();
                }
            }
        }

#if NET40
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ignore timer dispose, happens during finalize")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0067:Dispose objects before losing scope", Justification = "Ignore timer dispose, happens during finalize")]
        private static Task DelayNet40(int milliseconds)
        {
            var tcs = new TaskCompletionSource<object>();
            new Timer(_ => tcs.SetResult(null)).Change(milliseconds, -1);
            return tcs.Task;
        }
#endif

        private static Task Delay(long milliseconds)
        {
#if NETSTANDARD2_0
            return Task.Delay((int)milliseconds);
#else

            return DelayNet40((int)milliseconds);
#endif
        }

        private void ProcessCooldownList()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                var timestamp = timeSource.GetRelativeTimestamp();

                while (waitingQueue.Count > 0)
                {
                    var top = waitingQueue.Peek();
                    var targetTime = cooldownValues[top];

                    // not yet ready
                    if (targetTime > timestamp)
                    {
                        var timeToNextRelease = targetTime - timestamp;
                        ProcessCooldownAgainAfterMs(timeToNextRelease);
                        return;
                    }

                    waitingQueue.Dequeue();
                    cooldownValues.Remove(top);

                    readyQueue.Enqueue(top);
                    Monitor.PulseAll(sync);
                }
            }
        }

        private void ProcessCooldownAgainAfterMs(long milliseconds)
        {
            Delay(milliseconds).ContinueWith(_ => ProcessCooldownList(), TaskScheduler.Default);
        }

        private void AddOrUpdateBufferDictionary(TKey key, TValue value)
        {
            if (valueBuffer.ContainsKey(key))
            {
                valueBuffer[key] = value;
            }
            else
            {
                valueBuffer.Add(key, value);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ConcurrentBufferedQueue<TKey, TValue>));
            }
        }
    }
}
