# Objects and Data Structures

## Agent Summary

**Quick Rules:**
- Objects: Hide data, expose behavior
- Data Structures: Expose data, no behavior
- Don't create hybrids (half object, half data structure)
- Follow Law of Demeter (don't chain method calls)
- DTOs are pure data structures - no business logic

**Key Actions:**
1. Choose objects when adding new types
2. Choose data structures when adding new behaviors
3. Wrap third-party data structures
4. Use Tell-Don't-Ask principle
5. Keep DTOs simple and logic-free

---

Understanding the fundamental difference between objects and data structures is critical for clean code.

## Data Abstraction

Hiding implementation is not just putting a layer of functions between variables. It's about abstractions! A class exposes abstract interfaces that allow users to manipulate the *essence* of the data, without knowing its implementation.

### Bad Example - Concrete Point
```java
public class Point {
  public double x;
  public double y;
}
```

### Good Example - Abstract Point
```java
public interface Point {
  double getX();
  double getY();
  void setCartesian(double x, double y);
  double getR();
  double getTheta();
  void setPolar(double r, double theta);
}
```

The abstract point can be implemented as either Cartesian or Polar coordinates, and the user doesn't need to know which.

## Data/Object Anti-Symmetry

**Objects and data structures are opposites:**

### Data Structures
- Expose their data
- Have no meaningful functions
- Make it easy to add new functions without changing existing data structures
- Make it hard to add new data structures (all functions must change)

### Objects
- Hide their data behind abstractions
- Expose functions that operate on that data
- Make it easy to add new classes without changing existing functions
- Make it hard to add new functions (all classes must change)

### Procedural Shape Example (Data Structure)

```java
public class Square {
  public Point topLeft;
  public double side;
}

public class Rectangle {
  public Point topLeft;
  public double height;
  public double width;
}

public class Circle {
  public Point center;
  public double radius;
}

public class Geometry {
  public final double PI = 3.141592653589793;
  
  public double area(Object shape) throws NoSuchShapeException {
    if (shape instanceof Square) {
      Square s = (Square)shape;
      return s.side * s.side;
    }
    else if (shape instanceof Rectangle) {
      Rectangle r = (Rectangle)shape;
      return r.height * r.width;
    }
    else if (shape instanceof Circle) {
      Circle c = (Circle)shape;
      return PI * c.radius * c.radius;
    }
    throw new NoSuchShapeException();
  }
}
```

### Object-Oriented Shape Example (Objects)

```java
public interface Shape {
  double area();
}

public class Square implements Shape {
  private Point topLeft;
  private double side;
  
  public double area() {
    return side * side;
  }
}

public class Rectangle implements Shape {
  private Point topLeft;
  private double height;
  private double width;
  
  public double area() {
    return height * width;
  }
}

public class Circle implements Shape {
  private Point center;
  private double radius;
  public final double PI = 3.141592653589793;
  
  public double area() {
    return PI * radius * radius;
  }
}
```

## The Law of Demeter

A module should not know about the innards of the objects it manipulates.

### Rules
A method `f` of class `C` should only call methods of:
- `C` itself
- Objects created by `f`
- Objects passed as arguments to `f`
- Objects held in instance variables of `C`

**Don't call methods on objects returned by other methods.**

### Train Wrecks

Bad:
```java
String outputDir = ctxt.getOptions().getScratchDir().getAbsolutePath();
```

Better:
```java
Options opts = ctxt.getOptions();
File scratchDir = opts.getScratchDir();
String outputDir = scratchDir.getAbsolutePath();
```

But this still violates Demeter. Best solution depends on whether these are objects or data structures.

### Hybrids

Avoid creating hybrids - half object and half data structure. They:
- Have functions that do significant things
- Also have public variables or public accessors/mutators
- Get the worst of both worlds
- Make it hard to add new functions AND new data structures

### Hiding Structure

If `ctxt`, `Options`, and `ScratchDir` are objects, they should not expose their internals.

Bad:
```java
ctxt.getAbsolutePathOfScratchDirectoryOption();
```

This just moves the violation into `ctxt`.

Better - Tell, Don't Ask:
```java
BufferedOutputStream bos = ctxt.createScratchFileStream(classFileName);
```

This tells `ctxt` what to do rather than asking it questions about its internals.

## Data Transfer Objects (DTOs)

The quintessential form of a data structure is a class with:
- Public variables
- No functions

These are called Data Transfer Objects (DTOs).

### Bean Form
```java
public class Address {
  private String street;
  private String streetExtra;
  private String city;
  private String state;
  private String zip;
  
  public String getStreet() {
    return street;
  }
  
  public void setStreet(String street) {
    this.street = street;
  }
  
  // ... other getters and setters
}
```

### Active Record

Active Records are special DTOs with:
- Public (or bean-accessed) variables
- Navigational methods like `save` and `find`

**Treat Active Records as data structures.** Don't put business rules in them. Create separate objects for business rules that use the Active Record as a data source.

## Summary

### Choose the Right Tool

**Objects:** 
- Use when you want to add new types easily
- Use when behavior is more important than data
- Hide data, expose behavior

**Data Structures:**
- Use when you want to add new behaviors easily  
- Use when data is more important than behavior
- Expose data, have little or no behavior

### Key Principles

1. **Don't expose implementation details**
2. **Follow the Law of Demeter**
3. **Avoid hybrids**
4. **Know when to use objects vs data structures**
5. **Keep DTOs simple - just data, no business logic**

Mature programmers know that the idea that everything is an object is a myth. Sometimes you really do want simple data structures with procedures operating on them.