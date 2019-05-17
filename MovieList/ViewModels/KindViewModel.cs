using System;
using System.ComponentModel.DataAnnotations;

using HandyControl.Data;

using MovieList.Data.Models;
using MovieList.Properties;

namespace MovieList.ViewModels
{
    public class KindViewModel : ViewModelBase
    {
        public KindViewModel(Kind kind)
            => this.Kind = kind;

        public Kind Kind { get; }

        [Required(ErrorMessageResourceName = "NameRequired", ErrorMessageResourceType = typeof(Messages))]
        public string Name
        {
            get => this.Kind.Name;
            set
            {
                this.Kind.Name = value;
                this.OnPropertyChanged();
            }
        }

        public string ColorForMovie
        {
            get => this.Kind.ColorForMovie;
            set
            {
                this.Kind.ColorForMovie = value;
                this.OnPropertyChanged();
            }
        }

        public string ColorForSeries
        {
            get => this.Kind.ColorForSeries;
            set
            {
                this.Kind.ColorForSeries = value;
                this.OnPropertyChanged();
            }
        }

        public Func<string, OperationResult<bool>> VerifyName
            => this.Verify(nameof(Name));
    }
}
