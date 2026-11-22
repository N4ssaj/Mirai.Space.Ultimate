using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mirai.Space.Extensions.MVVM;

internal class LazyResolved<T>(IServiceProvider sp) 
    : Lazy<T>(sp.GetRequiredService<T>)
    where T : notnull
{

}
