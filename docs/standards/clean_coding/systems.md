# Systems

## Agent Summary

**Quick Rules:**
- Separate construction from use
- Use Dependency Injection
- Start simple, scale incrementally
- Keep domain logic clean of infrastructure
- Postpone architectural decisions

**Key Actions:**
1. Move all construction to main/startup
2. Inject dependencies, don't create them
3. Use factories for runtime object creation
4. Apply aspects for cross-cutting concerns
5. Create domain-specific languages

---

Software systems should separate the startup process from the runtime logic.

## Separate Constructing a System from Using It

**Construction is a very different process from use.**

Software systems should separate:
- **Startup process** - when application objects are constructed and dependencies are wired
- **Runtime logic** - that takes over after startup

### The Problem

Most applications don't separate these concerns:

```java
public Service getService() {
  if (service == null)
    service = new MyServiceImpl(...); // Hard-coded dependency
  return service;
}
```

Problems with lazy initialization:
- Violates Single Responsibility Principle
- MyServiceImpl is hard-coded dependency
- Testing requires test double for MyServiceImpl
- Must know global context
- All similar objects use same initialization

## Separation of Main

One way to separate construction from use is to move all construction to `main` or modules called by `main`.

```java
public class Main {
  public static void main(String[] args) {
    // Construct all objects here
    Database db = new MySQLDatabase(connectionString);
    UserRepository userRepo = new UserRepositoryImpl(db);
    UserService userService = new UserService(userRepo);
    OrderRepository orderRepo = new OrderRepositoryImpl(db);
    OrderService orderService = new OrderService(orderRepo, userService);
    
    // Wire up the application
    Application app = new Application(userService, orderService);
    
    // Start the runtime logic
    app.run();
  }
}
```

The main function builds the objects necessary for the system, then passes them to the application, which simply uses them.

## Factories

Sometimes we need to create objects after startup, based on runtime decisions.

### Abstract Factory Pattern

```java
public interface LineItemFactory {
  LineItem makeLineItem(Order order, Product product, int quantity);
}

public class OrderProcessing {
  private LineItemFactory lineItemFactory;
  
  public OrderProcessing(LineItemFactory factory) {
    this.lineItemFactory = factory;
  }
  
  public void processOrder(Order order) {
    for (Product product : order.getProducts()) {
      LineItem item = lineItemFactory.makeLineItem(
        order, product, product.getQuantity()
      );
      // Process the line item...
    }
  }
}
```

OrderProcessing doesn't know how LineItems are created - that's managed by the LineItemFactory implementation injected through main.

## Dependency Injection

A powerful mechanism for separating construction from use. It's an application of Inversion of Control (IoC) to dependency management.

### Key Concepts

- Objects should not instantiate dependencies themselves
- Pass instantiation responsibility to another "authoritative" mechanism
- Setup is a global concern - usually handled by main or a special container

### Constructor Injection

```java
public class MovieLister {
  private MovieFinder finder;
  
  public MovieLister(MovieFinder finder) {
    this.finder = finder;
  }
  
  public Movie[] moviesDirectedBy(String director) {
    List<Movie> allMovies = finder.findAll();
    // Filter for director...
  }
}
```

### Setter Injection

```java
public class MovieLister {
  private MovieFinder finder;
  
  public void setFinder(MovieFinder finder) {
    this.finder = finder;
  }
  
  public Movie[] moviesDirectedBy(String director) {
    List<Movie> allMovies = finder.findAll();
    // Filter for director...
  }
}
```

### Interface Injection

```java
public interface InjectFinder {
  void injectFinder(MovieFinder finder);
}

public class MovieLister implements InjectFinder {
  private MovieFinder finder;
  
  public void injectFinder(MovieFinder finder) {
    this.finder = finder;
  }
  
  public Movie[] moviesDirectedBy(String director) {
    List<Movie> allMovies = finder.findAll();
    // Filter for director...
  }
}
```

## Scaling Up

Software systems are unique compared to physical systems. Software architectures can grow incrementally, if we maintain proper separation of concerns.

### Test Drive the System Architecture

Start simple and grow:
1. Start with simple architecture
2. Implement stories quickly
3. Add more infrastructure as needed
4. Decouple architecture from domain logic
5. Test-drive architecture changes

"An optimal architecture consists of modular domains of concern, each implemented with Plain Old Java Objects. The different domains are integrated together with minimally invasive Aspects or Aspect-like tools."

## Cross-Cutting Concerns

Some concerns cut across natural object boundaries (logging, persistence, security, transactions).

### Aspect-Oriented Programming (AOP)

AOP provides approaches to cross-cutting concerns:

```java
@Entity
@Table(name="BANKS")
public class Bank {
  @Id @GeneratedValue(strategy=GenerationType.AUTO)
  private int id;
  
  @Column(name="NAME")
  private String name;
  
  // Business logic here
  public void transfer(Account from, Account to, Money amount) {
    // Domain logic
  }
}
```

The persistence is handled by attributes/annotations - the business logic is clean.

### Java Proxies

Simple situations can use Java proxies:

```java
public interface Bank {
  void transfer(Account from, Account to, Money amount);
}

public class BankImpl implements Bank {
  public void transfer(Account from, Account to, Money amount) {
    // Domain logic only
  }
}

public class BankProxyHandler implements InvocationHandler {
  private Bank bank;
  
  public BankProxyHandler(Bank bank) {
    this.bank = bank;
  }
  
  public Object invoke(Object proxy, Method method, Object[] args) throws Throwable {
    if (method.getName().equals("transfer")) {
      // Start transaction
      try {
        Object result = method.invoke(bank, args);
        // Commit transaction
        return result;
      } catch (Exception e) {
        // Rollback transaction
        throw e;
      }
    }
    return method.invoke(bank, args);
  }
}

// Usage
Bank bank = new BankImpl();
Bank proxyBank = (Bank) Proxy.newProxyInstance(
  Bank.class.getClassLoader(),
  new Class[] { Bank.class },
  new BankProxyHandler(bank)
);
```

### Pure Java AOP Frameworks

Spring AOP and JBoss AOP provide cleaner approaches:

```xml
<beans>
  <bean id="bankDataSource" class="org.apache.commons.dbcp.BasicDataSource">
    <property name="driverClassName" value="com.mysql.jdbc.Driver"/>
    <property name="url" value="jdbc:mysql://localhost:3306/mydb"/>
    <property name="username" value="root"/>
  </bean>
  
  <bean id="bank" class="com.example.BankImpl">
    <property name="dataSource" ref="bankDataSource"/>
  </bean>
</beans>
```

## Optimize Decision Making

Postpone decisions until the last possible moment. This gives maximum information for the decision.

### Benefits of Postponement

- More information for better decisions
- Can experiment with different options
- Avoid premature optimization
- Keep options open

## Use Standards Wisely, When They Add Demonstrable Value

Standards make it easier to:
- Reuse ideas and components
- Recruit people with relevant experience
- Encapsulate good practices
- Wire components together

But creating standards takes time and may limit choices. Use when benefits are clear.

## Systems Need Domain-Specific Languages

DSLs allow all levels of abstraction to be expressed in the domain.

```java
// Example DSL for building SQL
Select.from("users")
  .where("age").greaterThan(18)
  .and("status").equals("active")
  .orderBy("name")
  .limit(10);
```

Good DSLs minimize the communication gap between domain concepts and implementation.

## Summary

### Key Principles

1. **Separate construction from use**
2. **Use Dependency Injection**
3. **Scale incrementally**
4. **Keep domain logic clean**
5. **Use aspects for cross-cutting concerns**
6. **Postpone architectural decisions**
7. **Use standards when beneficial**
8. **Create domain-specific languages**

### Architecture Best Practices

- Start simple
- Separate concerns
- Use POJOs for domain logic
- Integrate with minimal invasiveness
- Test-drive system architecture
- Postpone decisions for maximum information

Systems must be clean too. An invasive architecture overwhelms domain logic and impacts agility. When domain logic is obscured, quality suffers, bugs creep in, and stories become harder to implement.

**Whether you are designing systems or individual modules, never forget to use the simplest thing that can possibly work.**