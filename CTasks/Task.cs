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

namespace CTasks.Task
{
    public delegate void Executable();
    public delegate Executable Executable<in T>(T obj);
    public delegate void Handleable(int state, Exception[] exceptions);

    public class Task {
        public const int STATE_NONE = 0x0;
        public const int STATE_STARTED = 0x1 << 25;
        public const int STATE_DONE = STATE_STARTED | 0x1 << 31;
        public const int STATE_RUNNING = STATE_STARTED | 0x1 << 26;
        public const int STATE_CANCELED = STATE_DONE | 0x1 << 27;
        public const int STATE_SUCCESS = STATE_DONE | 0x1 << 28;
        public const int STATE_FAILED = STATE_DONE | 0x1 << 29;
        public const int STATE_POST_FAILED = STATE_DONE | 0x1 << 30;

        public readonly object mLock;
        public volatile Executable<Task> mExec;
        public volatile Handleable mPostExec;

        private volatile int mState;
        private volatile Exception[] mExcepts;

        public Task() : this(null, null, null) {}
        public Task(Executable<Task> exec) : this(exec, null, null) {}
        public Task(Executable<Task> exec, Handleable postExec) : this(exec, postExec, null) {}
        protected Task(Executable<Task> exec, Handleable postExec, object lockObj) {
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
            return (GetState() & STATE_STARTED) == STATE_STARTED;
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

        public virtual bool Execute() {
            Executable taskExec = () => {
                bool skip;
                lock (mLock) {
                    int state = mState;
                    if ((state & STATE_STARTED) != STATE_STARTED) return;
                    if ((state & STATE_DONE) == STATE_DONE) return;
                    if ((state & STATE_RUNNING) == STATE_RUNNING) return;
                    state |= STATE_RUNNING;
                    mState = state;
                    Monitor.PulseAll(mLock);

                    skip = (state
                            & (STATE_FAILED | STATE_SUCCESS)
                            & ~STATE_DONE
                    ) != 0;
                }

                Executable postRun = null;
                Handleable postHandle = null;
                Exception exception = null;
                bool success = false, end = false;
                try {
                    Executable<Task> exec = mExec;
                    if (exec == null && !skip) {
                        lock (mLock) {
                            Monitor.Wait(mLock, 20);
                            if ((mState & STATE_DONE) == STATE_DONE) return;
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

                        postRun = exec(this);
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
                        state |= STATE_DONE;
                        mState = state;
                        Monitor.PulseAll(mLock);
                    }

                    if (postHandle == null) {
                        postHandle = mPostExec;
                    }
                }
                if (end) return;

                Executable postR = success ? postRun : null;
                Handleable postH = postHandle;
                if ((!success || postR == null)
                        && postH == null) return;

                Executable postExec = () => {
                    if (postR != null) {
                        try {
                            postR();
                        } catch (Exception ex) {
                            lock (mLock) {
                                mState |= STATE_POST_FAILED;
                                AddCause(ex);
                                Monitor.PulseAll(mLock);
                            }
                        }
                    }
                    if (postH != null) {
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
                    }
                };

                try {
                    if (skip) postExec();
                    else OnPostExecute(postExec);
                } catch (Exception ex) {
                    lock (mLock) {
                        mState |= STATE_POST_FAILED;
                        AddCause(ex);
                        Monitor.PulseAll(mLock);
                    }
                }
            };

            lock (mLock) {
                int state = mState;
                if ((state & STATE_STARTED) == STATE_STARTED) goto result;
                state = STATE_STARTED;
                mState = state;
                mExcepts = null;
                Monitor.PulseAll(mLock);
            }

            try {
                OnExecute(taskExec);
                goto result;
            } catch (Exception ex) {
                lock (mLock) {
                    int state = mState;
                    if ((state & (STATE_DONE | STATE_RUNNING)) == STATE_STARTED) {
                        state |= STATE_FAILED & ~STATE_DONE;
                    } else goto result;
                    mState = state;
                    AddCause(ex);
                }
            }
            try {
                OnPostExecute(taskExec);
                goto result;
            } catch (Exception ex) {
                lock (mLock) {
                    int state = mState;
                    if ((state & (STATE_DONE | STATE_RUNNING)) == STATE_STARTED) {
                        state = (STATE_FAILED | STATE_POST_FAILED) & ~STATE_DONE;
                    } else goto result;
                    mState = state;
                    AddCause(ex);
                    Monitor.PulseAll(mLock);
                }
            }
            return false;

            result:
            lock (mLock) {
                return (mState & STATE_CANCELED) != STATE_CANCELED;
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

        protected virtual void OnExecute(Executable exec) {
            new Thread(() => exec()).Start();
        }

        protected virtual void OnPostExecute(Executable exec) {
            exec();
        }
    }

    public class TaskStack {
        public readonly Object mLock;

        private List<Task> mTasks;

        public TaskStack() : this(null) {}
        protected TaskStack(Object lockObj) {
            if (lockObj == null) {
                lockObj = this;
            }

            mLock = lockObj;

            mTasks = new List<Task>(4);
        }

        public bool IsClosed() {
            return mTasks == null;
        }

        protected void ThrowIfClosed() {
            if (mTasks == null) {
                throw new InvalidOperationException("Task stack is closed");
            }
        }

        private List<Task> GetTasksOrThrow() {
            List<Task> tasks = mTasks;
            if (tasks == null) {
                throw new InvalidOperationException("Task stack is closed");
            }
            return tasks;
        }

        protected virtual void Update() {
            lock (mLock) {
                List<Task> tasks = GetTasksOrThrow();
                int len = tasks.Count;
                for (int i = 0; i < len; i++) {
                    Task task = tasks[i];
                    if (task != null
                            && task.IsStarted()
                            && !task.IsDone())
                        continue;
                    if (task != null) task.Cancel();
                    tasks.RemoveAt(i--);
                    len--;
                }
            }
        }

        public virtual int TaskCount() {
            lock (mLock) {
                List<Task> tasks = GetTasksOrThrow();
                if (tasks.Count > 0) Update();
                return tasks.Count;
            }
        }

        public virtual Task Execute(Executable<Task> exec = null,
                                    Handleable postExec = null) {
            lock (mLock) {
                Task task = Next(exec, postExec);
                task.Execute();
                return task;
            }
        }

        public virtual Task Next(Executable<Task> exec = null,
                                 Handleable postExec = null) {
            lock (mLock) {
                List<Task> tasks = GetTasksOrThrow();
                if (tasks.Count > 0) Update();

                Task task = new TaskImpl(this, exec, postExec);
                tasks.Add(task);
                return task;
            }
        }

        public TaskStack NotifyTasks() {
            lock (mLock) {
                Monitor.PulseAll(mLock);
                return this;
            }
        }

        public virtual Task GetPrimaryTask() {
            lock (mLock) {
                List<Task> tasks = GetTasksOrThrow();
                if (tasks.Count > 0) Update();
                int len = tasks.Count;
                return len > 0 ? tasks[len - 1] : null;
            }
        }

        public virtual bool Cancel() {
            lock (mLock) {
                List<Task> tasks = GetTasksOrThrow();
                int len = tasks.Count;
                if (len <= 0) return false;
                Task task = tasks[len - 1];
                tasks.RemoveAt(len - 1);
                if (task == null) return false;
                return task.Cancel();
            }
        }

        public virtual TaskStack CancelAll() {
            lock (mLock) {
                List<Task> tasks = GetTasksOrThrow();
                int len = tasks.Count;
                if (len <= 0) return this;

                for (int i = 0; i < len; i++) {
                    Task task = tasks[i];
                    if (task == null) continue;
                    task.Cancel();
                }
                tasks.Clear();
                return this;
            }
        }

        public virtual TaskStack CancelPrevious() {
            lock (mLock) {
                List<Task> tasks = GetTasksOrThrow();
                int len = tasks.Count;
                if (len <= 0) return this;

                Task primTask = tasks[--len];
                for (int i = 0; i < len; i++) {
                    Task task = tasks[i];
                    if (task == null) continue;
                    task.Cancel();
                }
                tasks.Clear();
                if (primTask != null) {
                    tasks.Add(primTask);
                }
                return this;
            }
        }

        public virtual void Close() {
            lock (mLock) {
                CancelAll();
                mTasks = null;
            }
        }

        protected virtual void OnExecute(Executable exec) {
            new Thread(() => exec()).Start();
        }

        protected virtual void OnPostExecute(Executable exec) {
            exec();
        }

        private class TaskImpl : Task {
            private readonly TaskStack mStack;

            public TaskImpl(TaskStack stack, Executable<Task> exec, Handleable postExec) 
                : base(exec, postExec, stack.mLock) {
                mStack = stack;
            }

            protected override void OnExecute(Executable exec) {
                mStack.OnExecute(exec);
            }

            protected override void OnPostExecute(Executable exec) {
                mStack.OnPostExecute(exec);
            }
        }
    }

    public class TaskSpawner {
        public readonly Object mLock;

        private bool mClosed;

        public TaskSpawner() : this(null) {}
        protected TaskSpawner(Object lockObj) {
            if (lockObj == null) {
                lockObj = this;
            }

            mLock = lockObj;

            mClosed = false;
        }

        public bool IsClosed() {
            return mClosed;
        }

        protected void ThrowIfClosed() {
            if (mClosed) {
                throw new InvalidOperationException("Task spawner is closed");
            }
        }

        public virtual Task Execute(Executable<Task> exec = null,
                                    Handleable postExec = null) {
            lock (mLock) {
                Task task = Spawn(exec, postExec);
                task.Execute();
                return task;
            }
        }

        public virtual Task Spawn(Executable<Task> exec = null,
                                  Handleable postExec = null) {
            lock (mLock) {
                ThrowIfClosed();
                return new TaskImpl(this, exec, postExec);
            }
        }

        public TaskSpawner NotifyTasks() {
            lock (mLock) {
                Monitor.PulseAll(mLock);
                return this;
            }
        }

        public virtual void Close() {
            lock (mLock) {
                mClosed = true;
            }
        }

        protected virtual void OnExecute(Executable exec) {
            new Thread(() => exec()).Start();
        }

        protected virtual void OnPostExecute(Executable exec) {
            exec();
        }

        private class TaskImpl : Task {
            private readonly TaskSpawner mSpawner;

            public TaskImpl(TaskSpawner spawner, Executable<Task> exec, Handleable postExec) 
                : base(exec, postExec, spawner.mLock) {
                mSpawner = spawner;
            }

            protected override void OnExecute(Executable exec) {
                mSpawner.OnExecute(exec);
            }

            protected override void OnPostExecute(Executable exec) {
                mSpawner.OnPostExecute(exec);
            }
        }
    }
}