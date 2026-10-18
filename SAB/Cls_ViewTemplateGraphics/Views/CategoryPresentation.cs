using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SAB.ViewTemplateGraphics.Models;

namespace SAB.ViewTemplateGraphics.Views
{
    public sealed class SearchActiveConverter:IValueConverter
    {
        public object Convert(object value,Type type,object parameter,CultureInfo culture)=>!string.IsNullOrWhiteSpace(value as string);
        public object ConvertBack(object value,Type type,object parameter,CultureInfo culture)=>Binding.DoNothing;
    }
    // Functional presentation only. Brushes and table/editor styles remain in SABFluentLight.
    public static class CategoryPresentation
    {
        public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(CategoryPresentation),new PropertyMetadata(false,Changed));
        public static bool GetEnabled(DependencyObject obj)=>(bool)obj.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject obj,bool value)=>obj.SetValue(EnabledProperty,value);
        static readonly ConditionalWeakTable<DataGrid,Session> sessions=new ConditionalWeakTable<DataGrid,Session>();
        static void Changed(DependencyObject obj,DependencyPropertyChangedEventArgs e)
        {
            if(!(obj is DataGrid grid)||!(bool)e.NewValue)return;
            var session=sessions.GetValue(grid,g=>new Session(g));
            grid.Loaded+=(s,a)=>session.Bind();grid.Unloaded+=(s,a)=>session.Unbind();
            grid.DataContextChanged+=(s,a)=>{if(grid.IsLoaded)session.Bind();};
        }
        public static bool Supports(CategoryOverrideRow row,string header)
        {
            switch(header) {
                case "Видимость":return row.AllowsVisibilityControl;
                case "Проекция\nЛинии":return row.SupportsProjectionLines;
                case "Проекция\nШтриховки":return row.SupportsSurfacePatterns;
                case "Прозрачность":return row.SupportsTransparency;
                case "Сечение\nЛинии":case "Сечение\nШтриховки":return row.SupportsCut;
                case "Полутона":return row.SupportsHalftone;
                case "Детализация":return row.SupportsDetailLevel;
                default:return true;
            }
        }
        public static bool SupportsEditor(CategoryOverrideRow row,GraphicOverrideEditorSection section)
        {
            switch(section) {
                case GraphicOverrideEditorSection.ProjectionLines:return row.SupportsProjectionLines;
                case GraphicOverrideEditorSection.SurfacePatterns:return row.SupportsSurfacePatterns;
                case GraphicOverrideEditorSection.Transparency:return row.SupportsTransparency;
                case GraphicOverrideEditorSection.CutLines:case GraphicOverrideEditorSection.CutPatterns:return row.SupportsCut;
                default:return false;
            }
        }
        public static void Refresh(DataGrid grid)
        {
            if(!(grid.DataContext is CategoryTabData tab))return;
            foreach(var column in grid.Columns) {
                var header=column.Header as string??"";
                bool show=tab.Rows.Count==0||header=="Категория"||header=="Статус"||tab.Rows.Any(row=>Supports(row,header));
                column.Visibility=show?Visibility.Visible:Visibility.Collapsed;
            }
            foreach(var guide in TemplateEditorPresentation.Children<TreeGuides>(grid))guide.InvalidateVisual();
        }
        sealed class Session
        {
            readonly DataGrid grid;CategoryTabData tab;readonly List<CategoryOverrideRow> rows=new List<CategoryOverrideRow>();
            public Session(DataGrid value){grid=value;}
            public void Bind(){Unbind();tab=grid.DataContext as CategoryTabData;if(tab==null)return;tab.Rows.CollectionChanged+=CollectionChanged;SubscribeRows();Refresh(grid);}
            void SubscribeRows(){foreach(var row in rows)row.PropertyChanged-=RowChanged;rows.Clear();rows.AddRange(tab.Rows);foreach(var row in rows)row.PropertyChanged+=RowChanged;}
            void CollectionChanged(object sender,NotifyCollectionChangedEventArgs e){SubscribeRows();Refresh(grid);}
            void RowChanged(object sender,PropertyChangedEventArgs e){Refresh(grid);}
            public void Unbind(){if(tab!=null)tab.Rows.CollectionChanged-=CollectionChanged;foreach(var row in rows)row.PropertyChanged-=RowChanged;rows.Clear();tab=null;}
        }
    }
    public sealed class TreeGuides:FrameworkElement
    {
        public TreeGuides(){IsHitTestVisible=false;DataContextChanged+=(s,e)=>InvalidateVisual();}
        protected override Size MeasureOverride(Size size)=>new Size(0,44);
        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);
            if(!(DataContext is CategoryOverrideRow row)||row.IndentLevel==0)return;
            DependencyObject current=this;while(current!=null&&!(current is DataGrid))current=VisualTreeHelper.GetParent(current);
            var tab=(current as DataGrid)?.DataContext as CategoryTabData;if(tab==null)return;
            var pen=new Pen((Brush)FindResource("SabBrush.Border"),1);
            double middle=ActualHeight/2;
            var chain=new List<CategoryOverrideRow>();var item=row;
            while(item.ParentCategoryIdValue!=-1) {
                item=tab.Rows.FirstOrDefault(x=>x.CategoryIdValue==item.ParentCategoryIdValue);
                if(item==null)break;chain.Insert(0,item);
            }
            for(int level=0;level<chain.Count-1;level++)
                if(FollowingSibling(tab,chain[level]))drawing.DrawLine(pen,new Point(level*24+14,0),new Point(level*24+14,ActualHeight));
            double x=(row.IndentLevel-1)*24+14;
            drawing.DrawLine(pen,new Point(x,0),new Point(x,FollowingSibling(tab,row)?ActualHeight:middle));
            drawing.DrawLine(pen,new Point(x,middle),new Point(row.IndentLevel*24+(row.HasChildren?14:26),middle));
        }
        public static bool FollowingSibling(CategoryTabData tab,CategoryOverrideRow row)
        {
            int index=tab.Rows.IndexOf(row);
            for(int i=index+1;i<tab.Rows.Count;i++)
                if(tab.Rows[i].ParentCategoryIdValue==row.ParentCategoryIdValue&&tab.Rows[i].IsVisibleInTree)return true;
            return false;
        }
    }
}
