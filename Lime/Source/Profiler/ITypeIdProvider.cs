using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lime.Profiler
{
	public interface ITypeIdProvider
	{
		int TypeId { get; }
	}
}
