from datetime import timedelta

import pandas as pd

def print_df(df, message=''):
    print('\n')
    print(message)
    print(df)
    return df

def top_n(df, column, n, ascending=True):
    data = df.nlargest(n, column, 'all')
    data = data.sort_values(column, ascending=ascending)
    return data

def test(df:pd.DataFrame):
    data = df[['joy_run_id', 'distance']]
    print_df(data, '2 cols')
    data = data.query('distance > 42')
    print_df(data, 'query > 42')
    data = data.groupby("joy_run_id").count()
    print_df(data, 'group and count')
    data = data.reset_index()
    print_df(data, 'reset index')
    data['rank'] = data['distance'].rank(method='max')
    print_df(data, 'rank max')
    data = data.sort_values('distance', ascending=False)
    print_df(data, 'sort')
    data = data.head(10)
    print_df(data, 'head')

def full_marathon(df:pd.DataFrame):
    data = df[['joy_run_id', 'distance']].query('distance > 42')
    data = data.groupby("joy_run_id").count()
    data = data.reset_index()
    data = top_n(data, 'distance', 10)
        
    return print_df(data, 'full marathon')

def distance(df:pd.DataFrame):
    data = df[['joy_run_id', 'distance']]
    data = data.groupby("joy_run_id").sum('distance')
    data = data.reset_index()
    data = top_n(data, 'distance', 10)
    
    return print_df(data, 'top distance')

def every_week(df:pd.DataFrame):
    data = df[['joy_run_id', 'week_no']]
    data = data.groupby("joy_run_id")['week_no'].nunique()
    data = data.reset_index(name='weeks')
    data = top_n(data, 'weeks', 1)
        
    print_df(data, 'every week run')

def __regular_pace_run(df:pd.DataFrame):
    slowest_pace = timedelta(minutes=10)
    data = df.query('pace <= @slowest_pace and run_type == "室外跑步"')

    #print_df(data, 'regular run')
    
    return data

def __avg_pace(row):
    total_time_secs = row['time'].total_seconds()
    total_distance = row['distance']
    avg_pace = timedelta(seconds = int(total_time_secs / total_distance))

    return avg_pace

def pace(df:pd.DataFrame):
    data = __regular_pace_run(df)
    data = data[['joy_run_id', 'time', 'distance']]
    data = data.groupby("joy_run_id").agg({'time':'sum','distance':'sum'})
    data = data.reset_index()  
    data = data.query('distance > 1500')
    # Debug mode, the amount of data is small
    # and there may be empty dataframe causes exception
    if data.empty:
        return data

    data['avg_pace'] = data.apply(__avg_pace, axis=1)
    data = data.nsmallest(10, 'avg_pace', 'all')
    data = data.sort_values('avg_pace', ascending=False)

    return print_df(data, 'top pace')

def total_time(df:pd.DataFrame):
    data = df[['joy_run_id', 'time']]
    #data = data.groupby("joy_run_id").sum('time')
    # timedelta can't be summed in this way 'sum()'
    data = data.groupby('joy_run_id').agg({'time': 'sum'})
    data = data.reset_index()
    data = top_n(data, 'time', 10)
    
    return print_df(data, 'top total time')

def total_days(df:pd.DataFrame):
    data = df[['joy_run_id', 'end_time']]
    data['end_date'] = data['end_time'].transform(lambda x : x.date())
    data = data[['joy_run_id', 'end_date']]
    data = data.groupby("joy_run_id")['end_date'].nunique()
    data = data.reset_index(name='days')
    data = top_n(data, 'days', 10)

    return print_df(data, 'top total days')

def top_stride_len(df:pd.DataFrame):
    data = __regular_pace_run(df)
    data = data.query('stride_len > 0 and stride_len < 1.8')
    data = data[['joy_run_id', 'stride_len', 'distance']]
    data = data.groupby('joy_run_id').agg({'stride_len':'mean','distance':'sum'})
    data = data.reset_index()  
    data = data.query('distance > 1500')
    data = top_n(data, 'stride_len', 10)

    return print_df(data, 'top stride len')

def __month_distance_sum(df:pd.DataFrame):
    data = df[['joy_run_id', 'end_time', 'distance']]
    data['month'] = data['end_time'].dt.to_period('M')
    data = data[['joy_run_id', 'month', 'distance']]  
    data = data.groupby(['joy_run_id', 'month'])['distance'].sum()
    data = data.reset_index()

    return data
    
def month_distance_std(df:pd.DataFrame):
    data = __regular_pace_run(df)
    data = __month_distance_sum(data)
    data = data.groupby('joy_run_id').agg({'month':'count','distance':'std'})
    data = data.reset_index()
    data = top_n(data, 'month', 1)
    data = data.nsmallest(10, 'distance', 'all')
    data = data.sort_values('distance', ascending=False)
        
    return print_df(data, 'month distance std')

def pace_std(df:pd.DataFrame):
    data = __regular_pace_run(df)
    data['pace_secs'] = data['pace'].dt.total_seconds()
    data = data.groupby('joy_run_id').agg({'distance':'sum','pace_secs':'std'})
    data = data.reset_index()
    data = data.query('distance > 1500')
    data = data.nsmallest(10, 'pace_secs', 'all')
    data = data.sort_values('pace_secs', ascending=False)
        
    return print_df(data, 'pace std')

