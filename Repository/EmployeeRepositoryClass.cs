using Contracts;
using Entities;
using Entities.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Repository
{
    class EmployeeRepositoryClass : RepositoryBase<Employee>, IEmployeeRepository
    {
        public EmployeeRepositoryClass (RepositoryContext repositoryContext) : base(repositoryContext)
        {

        }
    }
}
