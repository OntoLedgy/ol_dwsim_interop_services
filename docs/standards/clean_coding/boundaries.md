# Boundaries

## Agent Summary

**Quick Rules:**
- Wrap third-party code
- Write learning tests for APIs
- Depend on abstractions you control
- Define interfaces for code that doesn't exist yet
- Use Adapter pattern at boundaries

**Key Actions:**
1. Create wrapper classes for third-party APIs
2. Write learning tests to understand external code
3. Define your own interfaces
4. Isolate third-party code to specific modules
5. Create adapters to convert between interfaces

---

We seldom control all the software in our systems. We must cleanly integrate foreign code (third-party packages, open source, other teams' components) with our own.

## Using Third-Party Code

Natural tension exists between:
- **Providers:** Want broad applicability for wide audience
- **Users:** Want focused interface for specific needs

This tension causes problems at system boundaries.

### Example: java.util.Map

Map has a broad interface with many methods:
- clear()
- containsKey(Object key)
- containsValue(Object value)
- entrySet()
- equals(Object o)
- get(Object key)
- getClass()
- hashCode()
- isEmpty()
- keySet()
- put(Object key, Object value)
- putAll(Map t)
- remove(Object key)
- size()
- values()

### Bad - Passing Map Around
```java
Map sensors = new HashMap();
// ...
Sensor s = (Sensor)sensors.get(sensorId);
```

Problems:
- Map<String, Sensor> provides more capability than needed
- Requires casting at usage
- Not clear that only Sensors should be in the map

### Good - Wrapping Third-Party Code
```java
public class Sensors {
  private Map sensors = new HashMap();
  
  public Sensor getById(String id) {
    return (Sensor) sensors.get(id);
  }
  
  public void add(Sensor sensor) {
    sensors.put(sensor.getId(), sensor);
  }
  
  public boolean contains(String id) {
    return sensors.containsKey(id);
  }
  
  // Only expose needed functionality
}
```

Benefits:
- Interface tailored to application needs
- Easy to evolve with minimal impact
- Casting and type management handled internally
- Can enforce design and business rules
- Easy to mock for testing

## Exploring and Learning Boundaries

### Learning Tests

Instead of experimenting with new third-party code in our production code, write tests to explore our understanding of the API.

**Jim Newkirk calls these "learning tests".**

#### Example: Learning log4j

```java
public class LogTest {
  private Logger logger;
  
  @Before
  public void initialize() {
    logger = Logger.getLogger("logger");
    logger.removeAllAppenders();
    Logger.getRootLogger().removeAllAppenders();
  }
  
  @Test
  public void basicLogger() {
    BasicConfigurator.configure();
    logger.info("basicLogger");
  }
  
  @Test
  public void addAppenderWithStream() {
    logger.addAppender(new ConsoleAppender(
      new PatternLayout("%p %t %m%n"),
      ConsoleAppender.SYSTEM_OUT));
    logger.info("addAppenderWithStream");
  }
  
  @Test
  public void addAppenderWithoutStream() {
    logger.addAppender(new ConsoleAppender(
      new PatternLayout("%p %t %m%n")));
    logger.info("addAppenderWithoutStream");
  }
  
  @Test
  public void contextsAreIndependent() {
    Logger logger1 = Logger.getLogger("logger1");
    Logger logger2 = Logger.getLogger("logger2");
    
    logger1.info("logger1 message");
    logger2.info("logger2 message");
    
    assertTrue(logger1 != logger2);
  }
}
```

## Learning Tests Are Better Than Free

Learning tests:
- Cost nothing (we had to learn the API anyway)
- Provide precise experiments for understanding
- Have positive return on investment
- Verify that third-party packages work as expected
- Alert us when new releases break our usage

When new releases come out, run learning tests to detect behavioral differences.

## Using Code That Does Not Yet Exist

Sometimes we need to work with modules that haven't been developed yet.

### Example: Transmitter System

Working on a communication system where the transmitter subsystem isn't defined yet:

1. **Define the interface we wish we had:**

```java
public interface Transmitter {
  void transmit(Frequency frequency, Stream data);
}
```

2. **Create a Fake Implementation for testing:**

```java
public class FakeTransmitter implements Transmitter {
  public void transmit(Frequency frequency, Stream data) {
    // Fake implementation for testing
  }
}
```

3. **Use the interface in our code:**

```java
public class CommunicationController {
  private Transmitter transmitter;
  
  public CommunicationController(Transmitter transmitter) {
    this.transmitter = transmitter;
  }
  
  public void communicate(Frequency freq, Stream data) {
    // Our code using the interface we defined
    transmitter.transmit(freq, data);
  }
}
```

4. **Create an Adapter when the API is defined:**

```java
public class TransmitterAdapter implements Transmitter {
  private RealTransmitterAPI api;
  
  public void transmit(Frequency frequency, Stream data) {
    // Adapt our interface to the real API
    api.send(frequency.getValue(), data.getBytes());
  }
}
```

Benefits:
- We control our interface
- Testing is easier with our fake
- We're not blocked by the other team
- When API changes, only adapter needs modification

## Clean Boundaries

Good software designs accommodate change without huge investments and rework.

### Best Practices

1. **Limit third-party code to well-defined boundaries**
2. **Wrap third-party APIs**
   - Easier to change libraries
   - Easier to mock for testing
   - Not tied to particular vendor's API design

3. **Use the Adapter pattern** to convert from our interface to the provided interface

4. **Avoid letting too much of our code know about third-party particulars**

5. **Create predicates and adapters** for boundaries that don't exist yet

6. **Write learning tests** for third-party code

### Example: Wrapping a Database Layer

Instead of using Hibernate throughout the code:

```java
// Bad - Hibernate scattered throughout
public class UserService {
  public User getUser(Long id) {
    Session session = HibernateUtil.getSessionFactory().openSession();
    User user = (User) session.get(User.class, id);
    session.close();
    return user;
  }
}
```

Wrap it:

```java
// Good - Database details hidden
public interface UserRepository {
  User findById(Long id);
  void save(User user);
  void delete(User user);
}

public class HibernateUserRepository implements UserRepository {
  public User findById(Long id) {
    Session session = HibernateUtil.getSessionFactory().openSession();
    User user = (User) session.get(User.class, id);
    session.close();
    return user;
  }
  // ... other methods
}

public class UserService {
  private UserRepository repository;
  
  public User getUser(Long id) {
    return repository.findById(id);
  }
}
```

## Summary

### Key Principles

1. **Wrap third-party code** to minimize dependencies
2. **Write learning tests** to understand APIs
3. **Define interfaces for code that doesn't exist**
4. **Use adapters** at boundaries
5. **Keep boundaries clean and well-defined**
6. **Depend on abstractions, not concrete third-party code**

Interesting things happen at boundaries. Change is one of those things. Good software designs accommodate change without huge investments and rework. When we use code that is out of our control, special care must be taken to protect our investment and make sure future change is not too costly.