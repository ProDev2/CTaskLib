/*
 * Copyright (c) 2021 GVoid (Pascal Gerner)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Threading;
using System.Collections.Generic;

namespace CTasks.Exec
{
    public delegate void Executable();
    public delegate void Executable<in T>(T obj);
    public delegate void Handleable(int state, Exception[] exceptions);

    public class Request {
        public const int STATE_NONE = 0x0;
        public const int STATE_STARTED = 0x1 << 25;
        public const int STATE_READY = STATE_STARTED | 0x1 << 24;
        public const int STATE_DONE = STATE_STARTED | 0x1 << 31;
        public const int STATE_RUNNING = STATE_READY | 0x1 << 26;
        public const int STATE_CANCELED = STATE_DONE | 0x1 << 27;
        public const int STATE_SUCCESS = STATE_DONE | 0x1 << 28;
        public const int STATE_FAILED = STATE_DONE | 0x1 << 29;
        public const int STATE_POST_FAILED = STATE_DONE | 0x1 << 30;

        public readonly object mLock;
        public volatile Executable<Request> mExec;
        public volatile Handleable mPostExec;
        
        private volatile int mState;
        private volatile Exception[] mExcepts;

        public Request() : this(null, null, null) {}
        public Request(Executable<Request> exec) : this(exec, null, null) {}
        public Request(Executable<Request> exec, Handleable postExec) : this(exec, postExec, null) {}
        protected Request(Executable<Request> exec, Handleable postExec, object lockObj) {
            if (lockObj == null) {
                lockObj = this;
            }

            mLock = lockObj;
            mExec = exec;
            mPostExec = postExec;

            lock (mLock) {
                mState = STATE_NONE;
                mExcepts = new Exception[0];
            }
        }

        public virtual int GetState() {
            return mState;
        }

        public bool IsState(int s) {
            return (GetState() & s) == s;
        }

        public bool IsStarted() {
            return (GetState() & STATE_READY) != 0;
        }

        public bool IsReady() {
            return (GetState() & STATE_READY) == STATE_READY;
        }

        public bool IsWaiting() {
            return (GetState()
                    & (STATE_RUNNING | STATE_DONE)
                    & ~STATE_READY
            ) == 0;
        }

        public bool IsRunning() {
            return (GetState() & STATE_RUNNING) == STATE_RUNNING;
        }

        public bool IsDone() {
            return (GetState() & STATE_DONE) == STATE_DONE;
        }

        public bool IsCanceled() {
            return (GetState() & STATE_CANCELED) == STATE_CANCELED;
        }

        public bool IsSuccess() {
            return (GetState() & STATE_SUCCESS & ~STATE_DONE) != 0;
        }

        public bool IsFailed() {
            return (GetState() & STATE_FAILED & ~STATE_DONE) != 0;
        }

        public bool IsFailedPost() {
            return (GetState() & STATE_POST_FAILED & ~STATE_DONE) != 0;
        }

        public virtual Exception[] GetCauses() {
            return mExcepts;
        }

        private void AddCause(Exception except) {
            if (except == null) return;
            int len = mExcepts.Length;
            Exception[] nextExcepts = new Exception[len + 1];
            Array.Copy(mExcepts, 0, nextExcepts, 0, len);
            nextExcepts[len] = except;
            mExcepts = nextExcepts;
        }

        public virtual void Start() {
            lock (mLock) {
                int state = mState;
                if ((state & STATE_READY) != 0) return;
                state = STATE_STARTED;
                mState = state;
                mExcepts = null;
                Monitor.PulseAll(mLock);
            }
        }

        public virtual bool Cancel() {
            lock (mLock) {
                int state = mState;
                if ((state & STATE_CANCELED) == STATE_CANCELED) return true;
                if ((state & STATE_DONE) == STATE_DONE) return false;
                state |= STATE_CANCELED;
                mState = state;
                Monitor.PulseAll(mLock);
                return true;
            }
        }

        public virtual bool Ready() {
            lock (mLock) {
                int state = mState;
                if ((state & STATE_READY) == STATE_READY) return true;
                if ((state & STATE_DONE) != STATE_STARTED) return false;
                state |= STATE_READY;
                state &= ~STATE_STARTED;
                mState = state;
            }

            Exception exception = null;
            bool ready = false, failed = false;
            try {
                ready = OnPrepare();
            } catch (Exception ex) {
                failed = true;
                exception = ex;
            } finally {
                lock (mLock) {
                    int state = mState & ~STATE_READY;
                    state |= STATE_STARTED;
                    if ((state & STATE_DONE) == STATE_DONE) {
                        ready = false;
                    } else if (ready) {
                        state |= STATE_READY;
                    } else if (ready = failed) {
                        state |= STATE_READY;
                        state |= STATE_FAILED & ~STATE_DONE;
                        AddCause(exception);
                    }
                    mState = state;
                    Monitor.PulseAll(mLock);
                }
            }
            return ready;
        }

        public virtual bool Execute() {
            bool skip;
            lock (mLock) {
                int state = mState;
                if ((state
                        & (STATE_RUNNING | STATE_DONE)
                ) != STATE_READY) return false;
                state |= STATE_RUNNING;
                mState = state;
                Monitor.PulseAll(mLock);

                skip = (state
                        & (STATE_FAILED | STATE_SUCCESS)
                        & ~STATE_DONE
                ) != 0;
            }

            Handleable postHandle = null;
            Exception exception = null;
            bool success = false, end = false;
            try {
                Executable<Request> exec = mExec;
                if (exec == null && !skip) {
                    lock (mLock) {
                        Monitor.Wait(mLock, 20);
                        if ((mState & STATE_DONE) == STATE_DONE) return false;
                        exec = mExec;
                        postHandle = mPostExec;
                    }
                } else {
                    postHandle = mPostExec;
                }

                if (!skip) {
                    if (exec == null) {
                        throw new NullReferenceException("No executable attached");
                    }

                    exec(this);
                    success = true;
                }
            } catch (Exception ex) {
                exception = ex;
            } finally {
                lock (mLock) {
                    int state = mState;
                    if (exception is ThreadInterruptedException) {
                        state |= STATE_CANCELED;
                    }

                    if ((state & STATE_DONE) == STATE_DONE) {
                        success = false;
                        end = true;
                    } else if (!skip) {
                        state |= success ? STATE_SUCCESS : STATE_FAILED;
                        AddCause(exception);
                    }

                    state &= ~STATE_RUNNING;
                    state |= STATE_READY | STATE_DONE;
                    mState = state;
                    Monitor.PulseAll(mLock);
                }

                if (postHandle == null) {
                    postHandle = mPostExec;
                }
            }
            if (end) return false;

            Handleable postH = postHandle;
            if (postH == null) return success;

            Executable postExec = () => {
                try {
                    int st;
                    Exception[] ex;
                    lock (mLock) {
                        st = mState;
                        ex = mExcepts;
                    }
                    postH(st, ex);
                } catch (Exception ex) {
                    lock (mLock) {
                        mState |= STATE_POST_FAILED;
                        AddCause(ex);
                        Monitor.PulseAll(mLock);
                    }
                }
            };

            try {
                OnPostExecute(postExec);
            } catch (Exception ex) {
                lock (mLock) {
                    mState |= STATE_POST_FAILED;
                    AddCause(ex);
                    Monitor.PulseAll(mLock);
                }
            }

            return success;
        }

        protected virtual bool OnPrepare() {
            return true;
        }

        protected virtual void OnPostExecute(Executable exec) {
            exec();
        }
    }

    public class Handler {
        public static long RETRY_TIMEOUT = 20L;

        public readonly Object mLock;

        private volatile bool mBusy;
        private readonly DStack<Request> mTasks;
        private readonly List<Entry> mTimedTasks;

        private volatile bool mClosed;

        public Handler() : this(null) {}
        protected Handler(Object lockObj) {
            if (lockObj == null) {
                lockObj = this;
            }

            mLock = lockObj;

            lock (mLock) {
                mBusy = false;

                mTasks = new DStack<Request>();
                mTimedTasks = new List<Entry>(8);
            }

            mClosed = false;
        }

        public bool IsClosed() {
            return mClosed;
        }

        protected void ThrowIfClosed() {
            if (mClosed) {
                throw new InvalidOperationException("Handler is closed");
            }
        }

        public Request Post(Object runnable) {
            return Push(runnable, null, null);
        }

        public Request PostDelayed(Object runnable, long delay) {
            return Push(runnable, delay, null);
        }

        public Request PostAtTime(Object runnable, long time) {
            return Push(runnable, null, time);
        }

        protected virtual Request Push(Object runnable, long? delay, long? time) {
            ThrowIfClosed();

            if (runnable == null) {
                throw new NullReferenceException("No runnable object attached");
            }

            if (runnable is Executable<Request> rExec) {
                runnable = new Request(rExec);
            }
            if (runnable is Executable exec) {
                runnable = new Request((r) => exec());
            }

            Request request;
            if (runnable is Request) {
                request = (Request) runnable;
            } else {
                throw new ArgumentException("Invalid runnable object");
            }
            request.Start();

            if (delay == null && time == null) {
                Push(request);
            } else if (delay == null) {
                Push(new Entry(request, (long) time));
            } else if (time == null) {
                try {
                    time = GetTime() + (long) delay;
                } catch {}
                if (time == null) Push(request);
                else Push(new Entry(request, (long) time));
            } else {
                time += delay;
                Push(new Entry(request, (long) time));
            }
            return request;
        }

        private void Push(Request request) {
            lock (mLock) {
                if (mClosed) return;

                mTasks.Push(request);
                Monitor.PulseAll(mLock);
            }
        }

        private void Push(Entry entry) {
            lock (mLock) {
                if (mClosed) return;

                int index = mTimedTasks.BinarySearch(
                    entry, entry
                );
                if (index < 0) index = ~index;
                mTimedTasks.Insert(index, entry);
                Monitor.PulseAll(mLock);
            }
        }

        public List<Request> GetAll() {
            return GetAll(false);
        }

        public virtual List<Request> GetAll(bool excludeTimed) {
            List<Request> tmpTasks;
            lock (mLock) {
                int size = mTasks.Count;
                if (!excludeTimed) {
                    size += mTimedTasks.Count;
                }
                tmpTasks = new List<Request>(size);
                foreach (Request request in mTasks) {
                    if (request == null) continue;
                    tmpTasks.Add(request);
                }
                if (!excludeTimed) {
                    foreach (Entry entry in mTimedTasks) {
                        if (entry == null) continue;
                        tmpTasks.Add(entry.mRequest);
                    }
                }
            }
            return tmpTasks;
        }

        public void CancelAll() {
            CancelAll(false);
        }

        public virtual void CancelAll(bool excludeTimed) {
            lock (mLock) {
                List<Request> tmpTasks;
                try {
                    tmpTasks = GetAll(excludeTimed);
                } finally {
                    RemoveAll(excludeTimed);
                }

                foreach (Request request in tmpTasks) {
                    try {
                        request.Cancel();
                    } catch {}
                }
            }
        }

        public void RemoveAll() {
            RemoveAll(false);
        }

        public virtual void RemoveAll(bool excludeTimed) {
            lock (mLock) {
                mTasks.Clear();
                if (!excludeTimed) {
                    mTimedTasks.Clear();
                }
            }
        }

        internal Request Next(long timeout) {
            if (mClosed) return null;

            if (mBusy) goto wait;
            bool locked = false;
            try {
                Monitor.TryEnter(mLock, ref locked);
                if (!locked) goto wait;
                if (mBusy) goto wait;
                mBusy = true;
            } finally {
                if (locked) {
                    Monitor.Exit(mLock);
                }
            }

            try {
                Request request;
                bool retry = false;
                int size;
                Entry entry = null;
                lock (mLock) {
                    while ((size = mTimedTasks.Count) > 0) {
                        entry = mTimedTasks[size - 1];
                        if (entry != null
                                && IsValid(entry.mRequest)) break;
                        mTimedTasks.RemoveAt(size - 1);
                    }
                }
                if (size > 0) {
                    long remTime = 0L;
                    try {
                        long t = GetTime();
                        remTime = entry.GetRemTime(t);
                    } catch {}
                    if (remTime > 0L) {
                        timeout = timeout != -1L
                                ? Math.Min(timeout, remTime)
                                : remTime;
                        entry = null;
                        goto afterEntry;
                    }

                    bool removed = false;
                    lock (mLock) {
                        if (size > mTimedTasks.Count) goto afterRemove;
                        Entry tmpEntry = mTimedTasks[size - 1];
                        if (entry != tmpEntry) goto afterRemove;
                        mTimedTasks.RemoveAt(size - 1);
                        removed = true;
                    }

                    afterRemove:
                    try {
                        request = entry.mRequest;
                        if (request.Ready()) {
                            return request;
                        }
                    } catch {
                        removed = false;
                    }

                    retry = true;
                    if (!removed) entry = null;
                } else entry = null;

                afterEntry:
                if (mClosed) return null;

                request = null;
                lock (mLock) {
                    while ((size = mTasks.Count) > 0) {
                        request = mTasks.Pop();
                        if (request != null
                                && IsValid(request)) break;
                    }
                    if (entry != null && !mClosed) {
                        mTasks.Push(entry.mRequest);
                    }
                }
                if (size > 0) {
                    try {
                        if (request.Ready()) {
                            return request;
                        }
                    } catch {
                        goto afterRequest;
                    }
                    
                    retry = true;
                    lock (mLock) {
                        if (!mClosed) {
                            mTasks.Push(request);
                        }
                    }
                }

                afterRequest:
                if (!retry) goto wait;
                if (mClosed) return null;

                long retryTimeout = RETRY_TIMEOUT;
                timeout = timeout != -1L
                        ? Math.Min(timeout, retryTimeout)
                        : retryTimeout;
            } finally {
                mBusy = false;
            }

            wait:
            if (timeout >= -1L && !mClosed) {
                int iTimeout = (int) Math.Min(
                    (long) int.MaxValue, timeout
                );
                if (iTimeout == -1) {
                    iTimeout = Timeout.Infinite;
                }
                lock (mLock) {
                    Monitor.Wait(mLock, iTimeout);
                }
                return Next(-2L);
            } else {
                return null;
            }
        }

        public void Close() {
            lock (mLock) {
                mClosed = true;
                Monitor.PulseAll(mLock);
            }
            RemoveAll(false);
        }

        protected long GetTime() {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        protected static bool IsValid(Request request) {
            try {
                return request.IsWaiting()
                        && request.IsStarted();
            } catch {
                return false;
            }
        }

        private sealed class Entry : IComparer<Entry> {
            public readonly Request mRequest;
            public readonly long mAtTime;

            public Entry(Request request, long atTime) {
                mRequest = request;
                mAtTime = atTime;
            }

            public long GetRemTime(long time) {
                return Math.Max(mAtTime - time, 0L);
            }

            public int Compare(Entry e1, Entry e2) {
                long diff = e2.mAtTime - e1.mAtTime;
                diff /= diff < 0L ? -diff : diff;
                return (int) diff;
            }
        }
    }

    public class Looper {
        public const int STATE_NONE = 0x0;
        public const int STATE_STARTED = 0x1 << 25;
        public const int STATE_READY = STATE_STARTED | 0x1 << 24;

        public const long IMMEDIATE_TIMEOUT = -2L;
        public const long NO_TIMEOUT = -1L;

        public static long DEFAULT_TIMEOUT = 700L;

        public readonly Object mLock;
        public volatile Handler mHandler;
        public volatile FailHandler mFailHandler;

        private volatile int mState;

        public Looper() : this(null, null, null) {}
        public Looper(Handler handler) : this(handler, null, null) {}
        public Looper(Handler handler, FailHandler failHandler) : this(handler, failHandler, null) {}
        protected Looper(Handler handler, FailHandler failHandler, Object lockObj) {
            if (lockObj == null) {
                lockObj = this;
            }

            mLock = lockObj;
            mHandler = handler;
            mFailHandler = failHandler;

            lock (mLock) {
                mState = STATE_NONE;
            }
        }

        public virtual int GetState() {
            return mState;
        }

        public bool IsState(int s) {
            return (GetState() & s) == s;
        }

        public bool IsStarted() {
            return (GetState() & STATE_STARTED) == STATE_STARTED;
        }

        public bool IsReady() {
            return (GetState() & STATE_READY) == STATE_READY;
        }

        public virtual void Start() {
            lock (mLock) {
                int state = mState;
                if ((state & STATE_STARTED) == STATE_STARTED) return;
                state = STATE_READY;
                mState = state;
                Monitor.PulseAll(mLock);
            }
        }

        public virtual void Stop() {
            lock (mLock) {
                int state = mState;
                if ((state & STATE_STARTED) != STATE_STARTED) return;
                state &= ~STATE_READY;
                mState = state;
                Monitor.PulseAll(mLock);
            }

            Handler handler = mHandler;
            if (handler != null) {
                lock (handler.mLock) {
                    Monitor.PulseAll(handler.mLock);
                }
            }
        }

        public bool HandleNonBlocking() {
            try {
                return Handle(IMMEDIATE_TIMEOUT);
            } catch (ThreadInterruptedException) {
                return false;
            }
        }

        public bool HandleBlocking() {
            return Handle(DEFAULT_TIMEOUT);
        }

        public virtual bool Handle(long timeout) {
            Handler handler = mHandler;
            lock (mLock) {
                int state = mState;
                if ((state & STATE_READY) != STATE_READY) return false;
                state &= ~STATE_READY;
                if (handler != null && !handler.IsClosed()) {
                    state |= STATE_STARTED;
                }
                mState = state;
                Monitor.PulseAll(mLock);
                if ((state & STATE_STARTED) != STATE_STARTED) return false;
            }

            Request request;
            Exception exception = null;
            try {
                request = handler.Next(timeout);
                if (request == null) return false;

                lock (mLock) {
                    if ((mState & STATE_STARTED) != STATE_STARTED) goto failed;
                }

                try {
                    if (request.Execute()) return true;
                } catch (Exception ex) {
                    exception = ex;
                }
            } finally {
                handler = mHandler;
                lock (mLock) {
                    int state = mState;
                    if ((state & STATE_STARTED) == STATE_STARTED) {
                        if (handler == null || handler.IsClosed()) {
                            state &= ~STATE_READY;
                        } else state |= STATE_READY;
                        mState = state;
                        Monitor.PulseAll(mLock);
                    }
                }
            }

            failed:
            FailHandler failHandler = mFailHandler;
            if (failHandler != null) {
                try {
                    failHandler(request, exception);
                } catch {}
            }
            return true;
        }

        public virtual void Run() {
            long timeout = DEFAULT_TIMEOUT;
            timeout = Math.Max(timeout, -1L);

            try {
                while (IsReady()) {
                    Handle(timeout);
                }
            } catch (ThreadInterruptedException) {}
        }

        public static Thread StartOnThread(Looper looper) {
            looper.Start();
            Thread thread = new Thread(new ThreadStart(looper.Run));
            thread.Start();
            return thread;
        }

        public delegate void FailHandler(Request request, Exception exception);
    }
}