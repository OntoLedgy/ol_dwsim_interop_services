# Concurrency

## Agent Summary

**Quick Rules:**
- Keep concurrent code separate
- Severely limit access to shared data
- Use immutable objects when possible
- Keep synchronized sections small
- Threads should be independent

**Key Actions:**
1. Isolate concurrency code from business logic
2. Use thread-safe collections
3. Minimize shared mutable state
4. Test with more threads than processors
5. Run tests on different platforms
6. Assume things will go wrong

---

Writing clean concurrent programs is hard - very hard. It's much easier to write code that executes in a single thread. 

## Why Concurrency?

Concurrency is a **decoupling strategy**. It helps decouple **what** gets done from **when** it gets done.

Benefits:
- Can dramatically improve throughput
- Can improve structure of an application
- Makes system easier to understand
- Offers powerful ways to separate concerns

Example: A system that handles one user at a time vs multiple simultaneous users.

## Myths and Misconceptions

### Common Myths (FALSE)

- **Concurrency always improves performance**
  - Only when there's a lot of wait time that can be shared
  
- **Design does not change when writing concurrent programs**
  - Concurrent design is remarkably different from single-threaded
  
- **Understanding concurrency issues is not important with containers**
  - You still need to understand concurrent update and deadlock issues

### Reality (TRUE)

- **Concurrency incurs overhead** - both performance and code complexity
- **Correct concurrency is complex** - even for simple problems
- **Concurrency bugs aren't repeatable** - often ignored as one-offs
- **Concurrency requires fundamental design change**

## Concurrency Defense Principles

### Single Responsibility Principle

**Keep your concurrency-related code separate from other code.**

- Concurrency has its own life cycle
- Has its own challenges
- Complex enough to be reason for change
- Deserves to be separated

### Limit the Scope of Data

**Take data encapsulation to heart; severely limit access to shared data.**

```java
// Bad - public shared data
public class X {
  public int lastId;
}

// Good - synchronized access
public class X {
  private int lastId;
  
  public synchronized int getNextId() {
    return ++lastId;
  }
}
```

The more places shared data can be updated, the more likely:
- You'll forget to protect one or more places
- Duplication of effort to ensure everything is effectively synchronized
- Difficult to find sources of failures

### Use Copies of Data

Avoid sharing data when possible:
- Copy objects and treat them as read-only
- Copy objects, collect results from multiple threads, merge results
- Use immutable objects when possible

### Threads Should Be as Independent as Possible

**Attempt to partition data into independent subsets that can be operated on by independent threads.**

Example: Divide work into separate, non-interacting tasks:
```java
// Each thread processes its own subset
public void processRange(int start, int end) {
  for (int i = start; i < end; i++) {
    processItem(items[i]);
  }
}
```

## Know Your Library

### Thread-Safe Collections

Java offers thread-safe collection classes:

- `ConcurrentHashMap` - Better performance than `HashMap` with synchronization
- `CopyOnWriteArrayList` - Efficient when reads vastly outnumber writes
- `ConcurrentLinkedQueue` - Non-blocking queue
- `BlockingQueue` and relatives - Producer-consumer designs

### Know Your Execution Models

#### Producer-Consumer

One or more producer threads create work and place it in a queue. Consumer threads take work from queue and process it.

```java
BlockingQueue<Work> queue = new LinkedBlockingQueue<>();

// Producer
class Producer implements Runnable {
  public void run() {
    while (hasWork()) {
      queue.put(createWork());
    }
  }
}

// Consumer
class Consumer implements Runnable {
  public void run() {
    while (true) {
      Work work = queue.take();
      process(work);
    }
  }
}
```

#### Readers-Writers

Multiple threads can read shared resource, but writers require exclusive access.

```java
ReadWriteLock lock = new ReentrantReadWriteLock();

// Reader
lock.readLock().lock();
try {
  // Read shared resource
} finally {
  lock.readLock().unlock();
}

// Writer
lock.writeLock().lock();
try {
  // Write to shared resource
} finally {
  lock.writeLock().unlock();
}
```

#### Dining Philosophers

Threads compete for limited resources. Watch for:
- Deadlock
- Livelock
- Starvation

## Beware Dependencies Between Synchronized Methods

Dependencies between synchronized methods cause subtle bugs.

```java
// Dangerous - calling between synchronized methods
public class Example {
  private int value;
  
  public synchronized void incrementValue() {
    value++;
  }
  
  public synchronized int getValue() {
    return value;
  }
  
  public synchronized void doubleValue() {
    value = getValue() * 2; // Potential issue
  }
}
```

Recommendations:
- Avoid using more than one method on a shared object
- When you must use multiple methods:
  - Client-Based Locking
  - Server-Based Locking
  - Adapted Server (add locking method)

## Keep Synchronized Sections Small

The `synchronized` keyword creates locks. Locks create delays and overhead.

**Keep synchronized sections as small as possible.**

```java
// Bad - too much in synchronized block
public synchronized void doWork() {
  preparework();        // Doesn't need sync
  updateSharedState();  // Needs sync
  doMoreWork();        // Doesn't need sync
}

// Good - minimize synchronized section
public void doWork() {
  preparework();
  synchronized(this) {
    updateSharedState();
  }
  doMoreWork();
}
```

## Writing Correct Shut-Down Code Is Hard

Graceful shutdown is difficult to get correct:
- Deadlock during shutdown
- Threads not responding to shutdown signal
- Resources not properly released

Use proven patterns and careful design.

## Testing Threaded Code

**Testing does not guarantee correctness, but good tests can minimize risk.**

### Write Tests That Have the Potential to Expose Problems

- Don't ignore sporadic failures
- Don't assume one-time failures are cosmic rays
- Investigate all threading failures

### Make Your Threaded Code Pluggable

- Be able to run with various configurations:
  - One thread, several threads, varying number of threads
  - Real objects vs test doubles
  - Fast vs slow test doubles
  - Different processor counts

### Make Your Threaded Code Tunable

Allow performance characteristics to be tuned:
- Number of threads
- Size of queues
- Timeouts
- Thresholds

### Run with More Threads Than Processors

This will cause task swapping and increase the likelihood of finding problems.

### Run on Different Platforms

Different operating systems have different threading policies. Run tests on:
- Windows
- Linux
- macOS
- Different hardware configurations

### Instrument Your Code to Try and Force Failures

Add calls to methods like `Thread.yield()` or `Thread.sleep()` to change timing:

```java
public class ThreadJigglePoint {
  public static void jiggle() {
    if (random.nextBoolean()) {
      Thread.yield();
    }
  }
}

// Use in code
public void method() {
  ThreadJigglePoint.jiggle();
  // ... rest of method
}
```

## Common Concurrency Issues

### Deadlock

Threads waiting for each other to release resources.

```java
// Classic deadlock scenario
class A {
  synchronized void methodA(B b) {
    b.last();
  }
  synchronized void last() { }
}

class B {
  synchronized void methodB(A a) {
    a.last();
  }
  synchronized void last() { }
}
```

Prevention:
- Always acquire locks in the same order
- Use lock-free algorithms
- Use timeouts

### Livelock

Threads constantly changing state in response to other threads, but making no progress.

### Starvation

Some threads never get CPU time or resources.

## Method and Class Design for Concurrency

### Client-Based Locking

```java
public class Example {
  private final List<String> list = new ArrayList<>();
  
  public void addIfNotPresent(String item) {
    synchronized(list) {
      if (!list.contains(item)) {
        list.add(item);
      }
    }
  }
}
```

### Server-Based Locking

```java
public class StringList {
  private final List<String> list = new ArrayList<>();
  
  public synchronized void addIfNotPresent(String item) {
    if (!list.contains(item)) {
      list.add(item);
    }
  }
}
```

## Summary

### Key Principles

1. **Concurrency is hard** - respect its complexity
2. **Keep concurrent code separate** - SRP
3. **Limit access to shared data** - encapsulation
4. **Use copies of data** - avoid sharing
5. **Make threads independent** - minimize interaction
6. **Know your library** - use proven tools
7. **Keep synchronized sections small** - minimize contention
8. **Test thoroughly** - on multiple platforms
9. **Instrument code** - to expose problems

### Best Practices

- First, get code working without concurrency
- Then, carefully add concurrency
- Keep concurrency management separate from business logic
- Think about shut-down early
- Assume things will go wrong
- Test with more threads than processors
- Never ignore system failures

**Clean concurrent code is possible, but it requires discipline, knowledge, and careful attention to detail.**