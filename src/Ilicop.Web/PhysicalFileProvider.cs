using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web
{
    /// <summary>
    /// Provides read/write access to files in a predefined folder.
    /// </summary>
    public class PhysicalFileProvider : IFileProvider
    {
        private readonly IConfiguration configuration;
        private readonly string rootDirectoryEnvironmentKey;

        private bool initialized;

        /// <inheritdoc/>
        public DirectoryInfo HomeDirectory { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalFileProvider"/> at the given root directory path.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="rootDirectoryEnvironmentKey">The name of the environment variable containing the root directory path.</param>
        /// <exception cref="ArgumentNullException">If <see cref="configuration"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">If <see cref="rootDirectoryEnvironmentKey"/> is <c>null</c>.</exception>
        public PhysicalFileProvider(IConfiguration configuration, string rootDirectoryEnvironmentKey)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.rootDirectoryEnvironmentKey = rootDirectoryEnvironmentKey ?? throw new ArgumentNullException(nameof(rootDirectoryEnvironmentKey));
        }

        /// <inheritdoc/>
        public FileStream CreateFile(string file)
        {
            if (!initialized) throw new InvalidOperationException("The file provider needs to be initialized first.");
            return File.Create(Path.Combine(HomeDirectory.FullName, file));
        }

        /// <inheritdoc/>
        public virtual StreamReader OpenText(string file)
        {
            if (!initialized) throw new InvalidOperationException("The file provider needs to be initialized first.");
            return File.OpenText(Path.Combine(HomeDirectory.FullName, file));
        }

        /// <inheritdoc/>
        public bool Exists(string file)
        {
            if (!initialized) throw new InvalidOperationException("The file provider needs to be initialized first.");
            return File.Exists(Path.Combine(HomeDirectory.FullName, file));
        }

        /// <inheritdoc/>
        public virtual Task DeleteFileAsync(string file)
        {
            if (!initialized) throw new InvalidOperationException("The file provider needs to be initialized first.");
            return Task.Run(() => File.Delete(Path.Combine(HomeDirectory.FullName, file)));
        }

        /// <inheritdoc/>
        public virtual IEnumerable<string> GetFiles()
        {
            if (!initialized) throw new InvalidOperationException("The file provider needs to be initialized first.");
            return HomeDirectory.GetFiles().Select(x => x.Name);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentException">If <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
        public void Initialize(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentException("The specified id is not valid.", nameof(id));

            var rootDirectory = configuration.GetValue<string>(rootDirectoryEnvironmentKey);
            HomeDirectory = new DirectoryInfo(rootDirectory).CreateSubdirectory(id.ToString());

            initialized = true;
        }
    }
}
