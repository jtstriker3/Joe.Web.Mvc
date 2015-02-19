Joe.Web.Mvc
===========

MVC Base Controllers that utilize Joe.Business to add Default CRUD methods to any Controller
Just inherit from RepositoryController<TModel, TViewModel, TContext>() and you will have the default actions of Index,
Create, Update and Delete

####Example (From Context to Controller)

```
//inherit from Joe.MapBack.MapBackDbContext becasue it implements IDbViewContext For you.
//This uses the EntityFramework MapBackContext which simply Extends DbContext and implements IDbViewContext
//This can be found in the Joe.Map.EF Package
public Context : Joe.Map.EntityFramework.MapBackDbContext
{
  public DbSet<Person> People { get; set; }
  public DbSet<Job> Jobs { get; set; }
}

//Model Objects
public class Person 
{
  public int Id { get; set; }
  public String Name { get; set; }
  public virtual List<Job> Jobs { get; set; }
}

public class Job
{
  public int Id { get; set; }
  public String Name { get; set; }
  public virtual List<Person> People { get; set; }
}

//ViewModel or DTO Objects
public class PersonView
{
  public int Id { get; set; }
  public String Name { get; set; }
  public virtual IEnumerable<JobView> Jobs { get; set; }
  
  [AllValues(typeof(Job), "Jobs")]
  public IEnumerable<JobView> AllJobs { get; set; }
}

public class JobView
{
  //This is set by all values attribute
  public Boolean Included { get; set; }
  public int Id { get; set; }
  public String Name { get; set; }
  public virtual IEnumerable<PersonView> People { get; set; }
}

public class PersonRepository<TViewModel> : Joe.Business.Repository<Person, TViewModel>
{

}

//Standard MVC Controller
//If you post back to the Index Action with the Filter value set e.g. Filter=Joe
//Any Properties Specified in MVCOption will be built into a or Filter
//In This Case your results would be where Name = Joe
[MVCOptions("Name")]
public class PersonController : Joe.Web.Mvc.RepositoryController<Person, PersonView>
{
  //Don't Forget to pass in the Repo To use
  public PersonController() : base(new PersonRepository<PersonView>())
  {
  
  }
}

//API Controller
public class PersonController : Joe.Web.Mvc.BaseApiController<Person, PersonView>
{
  //Don't Forget to pass in the Repo To use
  public PersonController() : base(new PersonRepository<PersonView>())
  {
  
  }
}


//This is an example of a security provider used by Joe.Security
//Once you create this Joe.Security will pick it up as long as 
//you only have one class that implements ISecurityProvider
//No need to manually Register it.
public class SecurityProvider : Joe.Security.ISecurityProvider
{
  public Boolean IsUserInRole(params String[] roles)
  {
     foreach (var role in roles)
        if (!String.IsNullOrEmpty(role))
           if (Roles.IsUserInRole(role))
              return true;

    return false;
  }

  public String UserID
  {
     get
     {
        return Membership.GetUser().UserName;
     }
    set
    {

    }
  }
}

```

Aside from your Razor Views this is all the code you would need to Generate CRUD methods for a Person.

####Coming Soon
I have some code generation (T4 Templates) that I have written that will examine your Context and Generate Repositories, ViewModels including AllValues Properties and Controllers and Views For you.
A little bit more polishing and will publish them
