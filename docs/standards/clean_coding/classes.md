<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# Classes

## Agent Summary

**Quick Rules:**
- Always pluralise class names
- Small classes (< 200 lines)
- Single Responsibility Principle (one reason to change)
- High cohesion (methods use same variables)
- Open for extension, closed for modification
- Depend on abstractions, not concrete classes

**Key Actions:**
1. Split large classes into smaller ones
2. Extract classes when cohesion drops
3. Create interfaces for dependencies
4. Isolate change-prone code
5. Name classes with single responsibility in mind

---

## Class Organization

### Standard Java Convention
1. Static constants
2. Private static variables  
3. Private instance variables
4. Public functions
5. Private utilities called by public functions

**Stepdown Rule:** Public functions should be followed by the private utilities they call.

### Encapsulation

- Keep variables and utility functions private
- Sometimes make them protected for tests
- Loosen encapsulation as last resort
- Prefer better test strategies first

## Classes Should Be Small!

**First Rule:** Classes should be small.
**Second Rule:** Classes should be smaller than that.

### Measuring Class Size

Don't count lines - **count responsibilities**.

### Bad Example - Too Many Responsibilities
```java
public class SuperDashboard extends JFrame implements MetaDataUser {
  public String getCustomizerLanguagePath()
  public void setSystemConfigPath(String systemConfigPath) 
  public String getSystemConfigDocument()
  public void setSystemConfigDocument(String systemConfigDocument) 
  public boolean getGuruState()
  public boolean getNoviceState()
  public boolean getOpenSourceState()
  public void showObject(MetaObject object) 
  public void showProgress(String s)
  public boolean isMetadataDirty()
  public void setMetadataDirty(boolean isDirty)
  public Component getLastFocusedComponent()
  public void setLastFocused(Component lastFocused)
  public void setMouseSelectState(boolean isMouseSelected) 
  public boolean isMouseSelected()
  public LanguageManager getLanguageManager()
  public Project getProject()
  public Project getFirstProject()
  public Project getLastProject()
  public String getNewProjectName()
  public void setComponentSizes(Dimension dim)
  public String getCurrentDir()
  public void setCurrentDir(String newDir)
  public void updateStatus(int dotPos, int markPos)
  public Class[] getDataBaseClasses()
  public MetaObject getSelectedObject()
  public void deselectObjects()
  public void setProject(Project project)
  public void editorAction(String actionName, ActionEvent event) 
  public void setMode(int mode)
  public FileManager getFileManager()
  public void setFileManager(FileManager fileManager)
  public ConfigManager getConfigManager()
  public void setConfigManager(ConfigManager configManager) 
  public ClassLoader getClassLoader()
  public void setClassLoader(ClassLoader classLoader)
  public Properties getProps()
  public String getUserHome()
  public String getBaseDir()
  public int getMajorVersionNumber()
  public int getMinorVersionNumber()
  public int getBuildNumber()
  public MetaObject pasting(MetaObject target, MetaObject pasted, MetaProject project)
  public void processMenuItems(MetaObject metaObject)
  public void processMenuSeparators(MetaObject metaObject) 
  public void processTabPages(MetaObject metaObject)
  public void processPlacement(MetaObject object)
  public void processCreateLayout(MetaObject object)
  public void updateDisplayLayer(MetaObject object, int layerIndex) 
  public void propertyEditedRepaint(MetaObject object)
  public void processDeleteObject(MetaObject object)
  public boolean getAttachedToDesigner()
  public void processProjectChangedState(boolean hasProjectChanged) 
  public void processObjectNameChanged(MetaObject object)
  public void runProject()
  public void setAçowDragging(boolean allowDragging) 
  public boolean allowDragging()
  public boolean isCustomizing()
  public void setTitle(String title)
  public IdeMenuBar getIdeMenuBar()
  public void showHelper(MetaObject metaObject, String propertyName) 
  // ... many more methods
}
```

This class has **too many responsibilities**.

### The Single Responsibility Principle (SRP)

**A class should have one, and only one, reason to change.**

#### Identifying Responsibilities

Try to describe the class in 25 words without using "if", "and", "or", or "but".

Bad description:
> "SuperDashboard provides access to the component that last held the focus, AND it manages the high-level GUI configuration, AND it manages the project state."

### Good Example - Single Responsibility
```java
public class Version {
  public int getMajorVersionNumber()
  public int getMinorVersionNumber()
  public int getBuildNumber()
}
```

## Cohesion

Classes should have a small number of instance variables. Each method should manipulate one or more of those variables.

**High cohesion:** When methods and variables of a class are co-dependent and hang together as a logical whole.

### Example of High Cohesion
```java
public class Stack {
  private int topOfStack = 0;
  private List<Integer> elements = new LinkedList<Integer>();
  
  public int size() {
    return topOfStack;
  }
  
  public void push(int element) {
    topOfStack++;
    elements.add(element);
  }
  
  public int pop() throws PoppedWhenEmpty {
    if (topOfStack == 0)
      throw new PoppedWhenEmpty();
    int element = elements.remove(--topOfStack);
    return element;
  }
}
```

All methods use both variables - maximally cohesive.

## Maintaining Cohesion Results in Many Small Classes

When classes lose cohesion, split them!

### Before - Low Cohesion
```java
public class PrintPrimes {
  public static void main(String[] args) {
    final int M = 1000;
    final int RR = 50;
    final int CC = 4;
    final int ORDMAX = 30;
    int P[] = new int[M + 1];
    int PAGENUMBER;
    int PAGEOFFSET;
    int ROWOFFSET;
    int C;
    int J;
    int K;
    boolean JPRIME;
    int ORD;
    int SQUARE;
    int N = 0;
    int MULT[] = new int[ORDMAX + 1];
    
    // ... lots of code using all these variables
  }
}
```

### After - Split into Small, Cohesive Classes
```java
public class PrimePrinter {
  public static void main(String[] args) {
    final int NUMBER_OF_PRIMES = 1000;
    int[] primes = PrimeGenerator.generate(NUMBER_OF_PRIMES);
    
    final int ROWS_PER_PAGE = 50;
    final int COLUMNS_PER_PAGE = 4;
    RowColumnPagePrinter tablePrinter = 
      new RowColumnPagePrinter(ROWS_PER_PAGE, COLUMNS_PER_PAGE,
                                "The First " + NUMBER_OF_PRIMES + " Prime Numbers");
    tablePrinter.print(primes);
  }
}

public class PrimeGenerator {
  private static int[] primes;
  private static ArrayList<Integer> multiplesOfPrimeFactors;
  
  protected static int[] generate(int n) {
    primes = new int[n];
    multiplesOfPrimeFactors = new ArrayList<Integer>();
    set2AsFirstPrime();
    checkOddNumbersForSubsequentPrimes();
    return primes;
  }
  
  private static void set2AsFirstPrime() {
    primes[0] = 2;
    multiplesOfPrimeFactors.add(2);
  }
  
  private static void checkOddNumbersForSubsequentPrimes() {
    int primeIndex = 1;
    for (int candidate = 3; primeIndex < primes.length; candidate += 2) {
      if (isPrime(candidate))
        primes[primeIndex++] = candidate;
    }
  }
  
  private static boolean isPrime(int candidate) {
    if (isLeastRelevantMultipleOfNextPrimeFactor(candidate)) {
      multiplesOfPrimeFactors.add(candidate);
      return false;
    }
    return isNotMultipleOfAnyPreviousPrimeFactor(candidate);
  }
  // ... more small, focused methods
}

public class RowColumnPagePrinter {
  private int rowsPerPage;
  private int columnsPerPage;
  private int numbersPerPage;
  private String pageHeader;
  private PrintStream printStream;
  
  public RowColumnPagePrinter(int rowsPerPage, int columnsPerPage, String pageHeader) {
    this.rowsPerPage = rowsPerPage;
    this.columnsPerPage = columnsPerPage;
    this.pageHeader = pageHeader;
    numbersPerPage = rowsPerPage * columnsPerPage;
    printStream = System.out;
  }
  
  public void print(int data[]) {
    int pageNumber = 1;
    for (int firstIndexOnPage = 0; 
         firstIndexOnPage < data.length;
         firstIndexOnPage += numbersPerPage) {
      int lastIndexOnPage = Math.min(firstIndexOnPage + numbersPerPage - 1, data.length - 1);
      printPageHeader(pageHeader, pageNumber);
      printPage(firstIndexOnPage, lastIndexOnPage, data);
      printStream.println("\f");
      pageNumber++;
    }
  }
  
  private void printPage(int firstIndexOnPage, int lastIndexOnPage, int[] data) {
    int firstIndexOfLastRowOnPage = firstIndexOnPage + rowsPerPage - 1;
    for (int firstIndexInRow = firstIndexOnPage;
         firstIndexInRow <= firstIndexOfLastRowOnPage;
         firstIndexInRow++) {
      printRow(firstIndexInRow, lastIndexOnPage, data);
      printStream.println("");
    }
  }
  // ... more small, focused methods
}
```

## Organizing for Change

Classes should be open for extension but closed for modification (Open-Closed Principle).

### Bad - Violates OCP
```java
public class Sql {
  public Sql(String table, Column[] columns)
  public String create()
  public String insert(Object[] fields)
  public String selectAll()
  public String findByKey(String keyColumn, String keyValue)
  public String select(Column column, String pattern)
  public String select(Criteria criteria)
  public String preparedInsert()
  private String columnList(Column[] columns)
  private String valuesList(Object[] fields, final Column[] columns)
  private String selectWithCriteria(String criteria)
  private String placeholderList(Column[] columns)
}
```

Problems:
- Must change when adding new SQL statement types
- Must change when modifying SQL syntax
- Violates SRP - multiple reasons to change

### Good - Follows OCP
```java
abstract public class Sql {
  public Sql(String table, Column[] columns)
  abstract public String generate();
}

public class CreateSql extends Sql {
  public CreateSql(String table, Column[] columns)
  @Override public String generate()
}

public class SelectSql extends Sql {
  public SelectSql(String table, Column[] columns)
  @Override public String generate()
}

public class InsertSql extends Sql {
  public InsertSql(String table, Column[] columns, Object[] fields)
  @Override public String generate()
  private String valuesList(Object[] fields, final Column[] columns)
}

public class SelectWithCriteriaSql extends Sql {
  public SelectWithCriteriaSql(String table, Column[] columns, Criteria criteria)
  @Override public String generate()
}

public class SelectWithMatchSql extends Sql {
  public SelectWithMatchSql(String table, Column[] columns, Column column, String pattern)
  @Override public String generate()
}

public class FindByKeySql extends Sql {
  public FindByKeySql(String table, Column[] columns, String keyColumn, String keyValue)
  @Override public String generate()
}

public class PreparedInsertSql extends Sql {
  public PreparedInsertSql(String table, Column[] columns)
  @Override public String generate()
  private String placeholderList(Column[] columns)
}

public class Where {
  public Where(String criteria)
  public String generate()
}

public class ColumnList {
  public ColumnList(Column[] columns)
  public String generate()
}
```

Benefits:
- Each class has single responsibility
- Can add new SQL types without modifying existing classes
- Follows Open-Closed Principle
- Testable in isolation

## Isolating from Change

Concrete classes contain implementation details. Abstract classes represent concepts only.

### Dependency Inversion Principle (DIP)

Classes should depend upon abstractions, not concrete details.

### Example - Portfolio Management
```java
// Bad - Direct dependency on concrete class
public class Portfolio {
  private Stock stocks[];
  
  public int value() {
    int total = 0;
    for (Stock stock : stocks) {
      total += stock.shares * stock.price;
    }
    return total;
  }
}
```

Problems:
- Fixed to Stock implementation
- Hard to test when stock prices fluctuate

```java
// Good - Depend on abstraction
public interface StockExchange {
  int currentPrice(String symbol);
}

public class Portfolio {
  private StockExchange exchange;
  
  public Portfolio(StockExchange exchange) {
    this.exchange = exchange;
  }
  
  public int value() {
    int total = 0;
    for (Stock stock : stocks) {
      total += stock.shares * exchange.currentPrice(stock.symbol);
    }
    return total;
  }
}

// Easy to test with stub
public class FixedStockExchangeStub implements StockExchange {
  private Map<String, Integer> prices = new HashMap<>();
  
  public void fix(String symbol, int price) {
    prices.put(symbol, price);
  }
  
  public int currentPrice(String symbol) {
    return prices.get(symbol);
  }
}
```

## Summary

### Key Principles

1. **Classes should be small**
2. **Single Responsibility Principle - one reason to change**
3. **High cohesion - methods and variables hang together**
4. **Low coupling - minimize dependencies between classes**
5. **Open-Closed Principle - open for extension, closed for modification**
6. **Dependency Inversion Principle - depend on abstractions**

### Benefits of Small Classes

- Easier to understand
- Easier to test
- Easier to reuse
- Lower risk of change
- Better organization
- More modular design

Getting software to work and making software clean are two different activities. First make it work, then make it clean.