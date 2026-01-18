# Error Handling

## Agent Summary

**Quick Rules:**
- Use exceptions, not error codes
- Write try-catch-finally first
- Don't return null
- Don't pass null
- Provide context in exceptions
- Use unchecked exceptions

**Key Actions:**
1. Replace error codes with exceptions
2. Extract try/catch bodies into functions
3. Return empty collections instead of null
4. Throw exceptions instead of returning null
5. Create domain-specific exception classes
6. Use Special Case Pattern for expected "errors"

---

Error handling is important, but if it obscures logic, it's wrong. Clean error handling makes code more readable and robust.

## Use Exceptions Rather Than Return Codes

Return codes clutter the caller and lead to deeply nested structures.

### Bad - Return Codes
```java
public class DeviceController {
  public void sendShutDown() {
    DeviceHandle handle = getHandle(DEV1);
    // Check the state of the device
    if (handle != DeviceHandle.INVALID) {
      // Save the device status to the record field
      retrieveDeviceRecord(handle);
      // If not suspended, shut down
      if (record.getStatus() != DEVICE_SUSPENDED) {
        pauseDevice(handle);
        clearDeviceWorkQueue(handle);
        closeDevice(handle);
      } else {
        logger.log("Device suspended. Unable to shut down");
      }
    } else {
      logger.log("Invalid handle for: " + DEV1.toString());
    }
  }
}
```

### Good - Exceptions
```java
public class DeviceController {
  public void sendShutDown() {
    try {
      tryToShutDown();
    } catch (DeviceShutDownError e) {
      logger.log(e);
    }
  }
  
  private void tryToShutDown() throws DeviceShutDownError {
    DeviceHandle handle = getHandle(DEV1);
    DeviceRecord record = retrieveDeviceRecord(handle);
    pauseDevice(handle);
    clearDeviceWorkQueue(handle);
    closeDevice(handle);
  }
}
```

## Write Your Try-Catch-Finally Statement First

- Try blocks are like transactions
- Catch blocks must leave the program in a consistent state
- Start with try-catch-finally when writing code that could throw exceptions
- This helps define what the user should expect

### Example - Test Driven Development
```java
@Test(expected = StorageException.class)
public void retrieveSectionShouldThrowOnInvalidFileName() {
  sectionStore.retrieveSection("invalid - file");
}
```

Then implement:
```java
public List<RecordedGrip> retrieveSection(String sectionName) {
  try {
    FileInputStream stream = new FileInputStream(sectionName);
    // ... rest of logic
  } catch (FileNotFoundException e) {
    throw new StorageException("retrieval error", e);
  }
  return new ArrayList<RecordedGrip>();
}
```

## Use Unchecked Exceptions

Checked exceptions violate the Open/Closed Principle:
- A change to a low-level method signature forces changes up the call hierarchy
- All intervening methods must declare the exception
- Encapsulation is broken

For robust software, checked exceptions sometimes add value. For general application development, the dependency costs outweigh the benefits.

## Provide Context with Exceptions

Each exception should provide enough context to determine:
- Source and location of error
- The operation that failed
- Type of failure

Create informative error messages and pass them with exceptions.

```java
throw new StorageException("Failed to retrieve section: " + sectionName);
```

## Define Exception Classes in Terms of a Caller's Needs

Classification should be based on how exceptions are caught.

### Bad - Catching Many Exception Types
```java
ACMEPort port = new ACMEPort(12);

try {
  port.open();
} catch (DeviceResponseException e) {
  reportPortError(e);
  logger.log("Device response exception", e);
} catch (ATM1212UnlockedException e) {
  reportPortError(e);
  logger.log("Unlock exception", e);
} catch (GMXError e) {
  reportPortError(e);
  logger.log("Device response exception", e);
} finally {
  // ...
}
```

### Good - Wrapping Third-Party API
```java
public class LocalPort {
  private ACMEPort innerPort;
  
  public LocalPort(int portNumber) {
    innerPort = new ACMEPort(portNumber);
  }
  
  public void open() {
    try {
      innerPort.open();
    } catch (DeviceResponseException e) {
      throw new PortDeviceFailure(e);
    } catch (ATM1212UnlockedException e) {
      throw new PortDeviceFailure(e);
    } catch (GMXError e) {
      throw new PortDeviceFailure(e);
    }
  }
}
```

Usage becomes clean:
```java
LocalPort port = new LocalPort(12);
try {
  port.open();
} catch (PortDeviceFailure e) {
  reportError(e);
  logger.log(e.getMessage(), e);
} finally {
  // ...
}
```

## Define the Normal Flow

Sometimes you don't want exception handling to obscure the business logic.

### Bad - Exception Handling Clutters Logic
```java
try {
  MealExpenses expenses = expenseReportDAO.getMeals(employee.getID());
  m_total += expenses.getTotal();
} catch(MealExpensesNotFound e) {
  m_total += getMealPerDiem();
}
```

### Good - Special Case Pattern
```java
MealExpenses expenses = expenseReportDAO.getMeals(employee.getID());
m_total += expenses.getTotal();
```

With `ExpenseReportDAO` returning a special case object:
```java
public class PerDiemMealExpenses implements MealExpenses {
  public int getTotal() {
    // return the per diem default
  }
}
```

## Don't Return Null

Returning null creates work for callers and is error-prone.

### Bad - Returning Null
```java
public void registerItem(Item item) {
  if (item != null) {
    ItemRegistry registry = peristentStore.getItemRegistry();
    if (registry != null) {
      Item existing = registry.getItem(item.getID());
      if (existing.getBillingPeriod().hasRetailOwner()) {
        existing.register(item);
      }
    }
  }
}
```

One missing null check causes a NullPointerException.

### Good - Throw Exception or Return Special Case
```java
List<Employee> employees = getEmployees();
if (employees != null) {
  for(Employee e : employees) {
    totalPay += e.getPay();
  }
}
```

Better:
```java
List<Employee> employees = getEmployees();
for(Employee e : employees) {
  totalPay += e.getPay();
}

public List<Employee> getEmployees() {
  if (/* there are no employees */) {
    return Collections.emptyList();
  }
  // ...
}
```

## Don't Pass Null

Passing null into methods is worse than returning null.

### Bad - Passing Null
```java
public double xProjection(Point p1, Point p2) {
  return (p2.x - p1.x) * 1.5;
}
```

What happens if someone passes null? NullPointerException!

### Attempts to Fix
```java
public double xProjection(Point p1, Point p2) {
  if (p1 == null || p2 == null) {
    throw new InvalidArgumentException("Invalid argument for xProjection");
  }
  return (p2.x - p1.x) * 1.5;
}
```

Or using assertions:
```java
public double xProjection(Point p1, Point p2) {
  assert p1 != null : "p1 should not be null";
  assert p2 != null : "p2 should not be null";
  return (p2.x - p1.x) * 1.5;
}
```

Still doesn't solve the problem. The real solution is to forbid passing null by default.

## Summary

Clean code is readable, but it must also be robust. These are not conflicting goals.

### Key Principles

1. **Use exceptions, not return codes**
2. **Write try-catch-finally first**
3. **Use unchecked exceptions**
4. **Provide context with exceptions**
5. **Define exception classes by caller's needs**
6. **Define the normal flow (Special Case Pattern)**
7. **Don't return null**
8. **Don't pass null**

Error handling can be seen as a separate concern, something that is viewable independently of our main logic. When done well, it's clean and unobtrusive.